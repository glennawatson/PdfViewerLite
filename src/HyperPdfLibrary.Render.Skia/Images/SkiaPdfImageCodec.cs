// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using SkiaSharp;

namespace HyperPdfLibrary.Render.Skia.Images;

/// <summary>Runs Skia image codecs and resampling over caller-owned managed pixels.</summary>
public sealed class SkiaPdfImageCodec : IPdfImageCodec
{
    /// <summary>The bytes of a color pixel.</summary>
    private const int ColorBytes = 4;

    /// <summary>The JPEG quality from which chroma is kept at full resolution.</summary>
    private const int FullChromaQuality = 90;

    /// <summary>The highest JPEG quality.</summary>
    private const int MaxQuality = 100;

    /// <inheritdoc/>
    public unsafe bool TryDecodeJpeg(ReadOnlySpan<byte> encoded, PdfImagePixelLayout layout, Span<byte> pixels)
    {
        if (!Fits(layout, pixels.Length))
        {
            return false;
        }

        fixed (byte* input = encoded)
        {
            using var data = SKData.Create((nint)input, encoded.Length, null);
            using var stream = new SKMemoryStream(data);
            using var codec = SKCodec.Create(stream, out var opened);
            if (opened != SKCodecResult.Success || codec.Info.Width != layout.Width || codec.Info.Height != layout.Height)
            {
                return false;
            }

            fixed (byte* output = pixels)
            {
                var decoded = layout.Format == PdfImagePixelFormat.Rgb888x
                    ? new SKImageInfo(layout.Width, layout.Height, SKColorType.Rgba8888, SKAlphaType.Opaque)
                    : Info(layout);
                return codec.GetPixels(decoded, (nint)output) is SKCodecResult.Success or SKCodecResult.IncompleteInput;
            }
        }
    }

    /// <inheritdoc/>
    public unsafe byte[]? EncodeJpeg(ReadOnlySpan<byte> pixels, PdfImagePixelLayout layout, int quality)
    {
        if (!Fits(layout, pixels.Length))
        {
            return null;
        }

        var downsample = quality >= FullChromaQuality ? SKJpegEncoderDownsample.Downsample444 : SKJpegEncoderDownsample.Downsample420;
        fixed (byte* pointer = pixels)
        {
            using var pixmap = new SKPixmap(Info(layout), (nint)pointer, layout.Width * PixelBytes(layout.Format));
            using var data = pixmap.Encode(new SKJpegEncoderOptions(Math.Clamp(quality, 1, MaxQuality), downsample, SKJpegEncoderAlphaOption.Ignore));
            return data?.ToArray();
        }
    }

    /// <inheritdoc/>
    public unsafe bool Resample(ReadOnlySpan<byte> source, PdfImagePixelLayout layout, int width, int height, Span<byte> output)
    {
        var target = layout with { Width = width, Height = height };
        if (!Fits(layout, source.Length) || !Fits(target, output.Length))
        {
            return false;
        }

        var bytes = PixelBytes(layout.Format);
        fixed (byte* from = source)
        {
            fixed (byte* to = output)
            {
                using var sourceMap = new SKPixmap(Info(layout), (nint)from, layout.Width * bytes);
                using var targetMap = new SKPixmap(Info(target), (nint)to, width * bytes);
                return sourceMap.ScalePixels(targetMap, new(SKCubicResampler.Mitchell));
            }
        }
    }

    /// <summary>Checks dimensions, format and byte length before exposing a buffer to Skia.</summary>
    /// <param name="layout">The pixel dimensions and format.</param>
    /// <param name="length">The available bytes.</param>
    /// <returns>True when the complete layout fits.</returns>
    private static bool Fits(PdfImagePixelLayout layout, int length)
    {
        var bytes = PixelBytes(layout.Format);
        return layout.Width > 0 && layout.Height > 0 && bytes > 0 && (long)layout.Width * layout.Height <= length / bytes;
    }

    /// <summary>Gets the number of bytes in a pixel.</summary>
    /// <param name="format">The pixel format.</param>
    /// <returns>The byte count, or zero for an unsupported format.</returns>
    private static int PixelBytes(PdfImagePixelFormat format) => format switch
    {
        PdfImagePixelFormat.Gray8 => 1,
        PdfImagePixelFormat.Bgra8888 or PdfImagePixelFormat.Rgb888x => ColorBytes,
        _ => 0,
    };

    /// <summary>Describes managed pixel channels and alpha to Skia.</summary>
    /// <param name="layout">The dimensions and channel format.</param>
    /// <returns>The corresponding Skia image information.</returns>
    private static SKImageInfo Info(PdfImagePixelLayout layout) => new(
        layout.Width,
        layout.Height,
        layout.Format switch
        {
            PdfImagePixelFormat.Gray8 => SKColorType.Gray8,
            PdfImagePixelFormat.Bgra8888 => SKColorType.Bgra8888,
            _ => SKColorType.Rgb888x,
        },
        layout.Format == PdfImagePixelFormat.Bgra8888 ? SKAlphaType.Premul : SKAlphaType.Opaque);
}
