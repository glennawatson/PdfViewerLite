// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using SkiaSharp;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Pixel work for image re-encoding through SkiaSharp: unpacking samples into 8-bit grey or RGBX pixels, downsampling
/// with a box filter followed by a Mitchell cubic resample, encoding JPEG and decoding JPEG. Pixels are grey (one byte)
/// or RGBX (four bytes, the fourth ignored), the layouts SkiaSharp reads as <see cref="SKColorType.Gray8"/> and
/// <see cref="SKColorType.Rgb888x"/>.
/// </summary>
internal static class ImagePixels
{
    /// <summary>The bytes of an RGBX pixel.</summary>
    internal const int RgbxBytes = 4;

    /// <summary>The bytes of an RGB sample triple.</summary>
    private const int RgbBytes = 3;

    /// <summary>The index of blue in a pixel.</summary>
    private const int Blue = 2;

    /// <summary>The value of an opaque channel.</summary>
    private const byte Opaque = 0xFF;

    /// <summary>The JPEG quality from which chroma is kept at full resolution.</summary>
    private const int FullChromaQuality = 90;

    /// <summary>The highest JPEG quality.</summary>
    private const int MaxQuality = 100;

    /// <summary>The smallest shrink factor worth averaging whole blocks for before the cubic resample.</summary>
    private const int MinBoxFactor = 2;

    /// <summary>Gets the bytes per pixel of a pixel layout.</summary>
    /// <param name="components">One for grey, three for RGB.</param>
    /// <returns>1 or 4.</returns>
    internal static int PixelBytes(int components) => components == 1 ? 1 : RgbxBytes;

    /// <summary>Copies 8-bit samples into pixels: grey as is, RGB widened to RGBX.</summary>
    /// <param name="samples">The samples, rows packed.</param>
    /// <param name="components">One for grey, three for RGB.</param>
    /// <param name="pixelCount">The pixels.</param>
    /// <param name="pixels">Receives the pixels.</param>
    internal static void FromSamples(ReadOnlySpan<byte> samples, int components, int pixelCount, ref PooledBuffer pixels)
    {
        if (components == 1)
        {
            pixels.Write(samples[..pixelCount]);
            return;
        }

        var target = pixels.GetSpan(pixelCount * RgbxBytes);
        for (var i = 0; i < pixelCount; i++)
        {
            var source = samples.Slice(i * RgbBytes, RgbBytes);
            var pixel = target.Slice(i * RgbxBytes, RgbxBytes);
            pixel[0] = source[0];
            pixel[1] = source[1];
            pixel[Blue] = source[Blue];
            pixel[RgbBytes] = Opaque;
        }

        pixels.Advance(pixelCount * RgbxBytes);
    }

    /// <summary>Shrinks pixels to a target size: whole blocks averaged first, then a cubic resample to the exact size.</summary>
    /// <param name="source">The pixels.</param>
    /// <param name="size">The source size and layout.</param>
    /// <param name="targetWidth">The target width.</param>
    /// <param name="targetHeight">The target height.</param>
    /// <param name="output">Receives the target pixels.</param>
    /// <returns><see langword="true"/> when the resample ran.</returns>
    internal static bool Downsample(ReadOnlySpan<byte> source, PixelSize size, int targetWidth, int targetHeight, ref PooledBuffer output)
    {
        var factor = Math.Min(size.Width / targetWidth, size.Height / targetHeight);
        if (factor < MinBoxFactor)
        {
            return Resample(source, size, targetWidth, targetHeight, ref output);
        }

        var reduced = default(PooledBuffer);
        try
        {
            var boxed = BoxReduce(source, size, factor, ref reduced);
            return Resample(reduced.WrittenSpan, boxed, targetWidth, targetHeight, ref output);
        }
        finally
        {
            reduced.Dispose();
        }
    }

    /// <summary>Encodes pixels as JPEG.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="size">Their size and layout.</param>
    /// <param name="quality">The quality, 1 to 100.</param>
    /// <returns>The JPEG bytes, or <see langword="null"/> when encoding failed.</returns>
    internal static unsafe byte[]? EncodeJpeg(ReadOnlySpan<byte> pixels, PixelSize size, int quality)
    {
        var downsample = quality >= FullChromaQuality ? SKJpegEncoderDownsample.Downsample444 : SKJpegEncoderDownsample.Downsample420;
        fixed (byte* pointer = pixels)
        {
            using var pixmap = new SKPixmap(Info(size.Width, size.Height, size.Components), (nint)pointer, size.Width * PixelBytes(size.Components));
            using var data = pixmap.Encode(new SKJpegEncoderOptions(Math.Clamp(quality, 1, MaxQuality), downsample, SKJpegEncoderAlphaOption.Ignore));
            return data?.ToArray();
        }
    }

    /// <summary>Decodes a JPEG into grey or RGBX pixels.</summary>
    /// <param name="jpeg">The JPEG bytes.</param>
    /// <param name="components">One for grey, three for RGB.</param>
    /// <param name="pixels">Receives the pixels.</param>
    /// <param name="size">The decoded size.</param>
    /// <returns><see langword="true"/> when the JPEG decoded.</returns>
    internal static unsafe bool TryDecodeJpeg(ReadOnlySpan<byte> jpeg, int components, ref PooledBuffer pixels, out PixelSize size)
    {
        size = default;
        using var data = SKData.CreateCopy(jpeg);
        using var codec = SKCodec.Create(new SKMemoryStream(data), out var opened);
        if (opened != SKCodecResult.Success || codec.Info is not { Width: > 0, Height: > 0 })
        {
            return false;
        }

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, components == 1 ? SKColorType.Gray8 : SKColorType.Rgba8888, SKAlphaType.Opaque);
        var length = info.Width * info.Height * PixelBytes(components);
        var target = pixels.GetSpan(length);
        fixed (byte* pointer = target)
        {
            var result = codec.GetPixels(info, (nint)pointer);
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            {
                return false;
            }
        }

        pixels.Advance(length);
        size = new(info.Width, info.Height, components);
        return true;
    }

    /// <summary>Resamples pixels to an exact size with a Mitchell cubic filter.</summary>
    /// <param name="source">The pixels.</param>
    /// <param name="size">Their size and layout.</param>
    /// <param name="targetWidth">The target width.</param>
    /// <param name="targetHeight">The target height.</param>
    /// <param name="output">Receives the target pixels.</param>
    /// <returns><see langword="true"/> when the resample ran.</returns>
    private static unsafe bool Resample(ReadOnlySpan<byte> source, PixelSize size, int targetWidth, int targetHeight, ref PooledBuffer output)
    {
        var bytes = PixelBytes(size.Components);
        if (size.Width == targetWidth && size.Height == targetHeight)
        {
            output.Write(source[..(size.Width * size.Height * bytes)]);
            return true;
        }

        var length = targetWidth * targetHeight * bytes;
        var target = output.GetSpan(length);
        bool scaled;
        fixed (byte* from = source)
        {
            fixed (byte* to = target)
            {
                using var sourceMap = new SKPixmap(Info(size.Width, size.Height, size.Components), (nint)from, size.Width * bytes);
                using var targetMap = new SKPixmap(Info(targetWidth, targetHeight, size.Components), (nint)to, targetWidth * bytes);
                scaled = sourceMap.ScalePixels(targetMap, new(SKCubicResampler.Mitchell));
            }
        }

        output.Advance(length);
        return scaled;
    }

    /// <summary>Averages square blocks of pixels; blocks cut off by the edge average the pixels they have.</summary>
    /// <param name="source">The pixels.</param>
    /// <param name="size">Their size and layout.</param>
    /// <param name="factor">The block side.</param>
    /// <param name="output">Receives the reduced pixels.</param>
    /// <returns>The reduced size.</returns>
    private static PixelSize BoxReduce(ReadOnlySpan<byte> source, PixelSize size, int factor, ref PooledBuffer output)
    {
        var bytes = PixelBytes(size.Components);
        var width = (size.Width + factor - 1) / factor;
        var height = (size.Height + factor - 1) / factor;
        var target = output.GetSpan(width * height * bytes);
        Span<int> sums = stackalloc int[RgbxBytes];
        for (var y = 0; y < height; y++)
        {
            var top = y * factor;
            var bottom = Math.Min(top + factor, size.Height);
            for (var x = 0; x < width; x++)
            {
                var left = x * factor;
                var right = Math.Min(left + factor, size.Width);
                sums.Clear();
                SumBlock(source, size.Width * bytes, bytes, new(left, top, right, bottom), sums);
                var count = (right - left) * (bottom - top);
                var pixel = target.Slice(((y * width) + x) * bytes, bytes);
                for (var c = 0; c < bytes; c++)
                {
                    pixel[c] = (byte)((sums[c] + (count >> 1)) / count);
                }
            }
        }

        output.Advance(width * height * bytes);
        return new(width, height, size.Components);
    }

    /// <summary>Adds up each channel of a block.</summary>
    /// <param name="source">The pixels.</param>
    /// <param name="stride">The bytes per row.</param>
    /// <param name="bytes">The bytes per pixel.</param>
    /// <param name="block">The block.</param>
    /// <param name="sums">Receives each channel's sum.</param>
    private static void SumBlock(ReadOnlySpan<byte> source, int stride, int bytes, SKRectI block, Span<int> sums)
    {
        for (var row = block.Top; row < block.Bottom; row++)
        {
            var line = source.Slice((row * stride) + (block.Left * bytes), (block.Right - block.Left) * bytes);
            for (var i = 0; i < line.Length; i += bytes)
            {
                for (var c = 0; c < bytes; c++)
                {
                    sums[c] += line[i + c];
                }
            }
        }
    }

    /// <summary>Describes pixels to SkiaSharp.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="components">One for grey, three for RGB.</param>
    /// <returns>The image info.</returns>
    private static SKImageInfo Info(int width, int height, int components) =>
        new(width, height, components == 1 ? SKColorType.Gray8 : SKColorType.Rgb888x, SKAlphaType.Opaque);
}
