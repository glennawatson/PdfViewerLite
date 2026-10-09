// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Images.Jpeg;
using HyperPdfLibrary.Objects;
using SkiaSharp;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>
/// Decodes DCTDecode (JPEG) images the way PDF defines them: the decoder returns the component samples and the image's
/// colour space and /Decode array interpret them, as for any 8-bit image. Gray and ordinary YCbCr JPEGs go through
/// SkiaSharp (libjpeg-turbo) for speed. JPEGs with four components, and three-component JPEGs that must not be converted
/// from YCbCr (an Adobe transform of 0, or <c>/ColorTransform 0</c> without an Adobe marker), go through the managed
/// <see cref="JpegDecoder"/>, which applies no Adobe inversion. It also reads files SkiaSharp rejects. A JPEG neither can
/// read gives an image with <see cref="PdfImageData.UnsupportedCodec"/> set to <see cref="PdfImageCodec.Jpeg"/>.
/// </summary>
internal static class JpegImageDecoder
{
    /// <summary>What the repair log says about a JPEG that ends before its end-of-image marker.</summary>
    private const string TruncatedMessage = "A JPEG image ends without its end marker; the part that decoded is used.";

    /// <summary>The components of an RGB JPEG.</summary>
    private const int RgbComponents = 3;

    /// <summary>The components of a CMYK JPEG.</summary>
    private const int CmykComponents = 4;

    /// <summary>The bits of a JPEG sample.</summary>
    private const int SampleBits = 8;

    /// <summary>The offset of the red byte in a BGRA pixel.</summary>
    private const int RedIndex = 2;

    /// <summary>Decodes a JPEG image with no <c>/ColorTransform</c> entry.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JPEG data.</param>
    /// <returns>The image, <see langword="null"/> when it is too large, or an image marked unsupported when no decoder can read it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? Decode(in ImageHeader header, ReadOnlySpan<byte> data) =>
        Decode(header, data, JpegInfo.NoColorTransform);

    /// <summary>Decodes a JPEG image.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JPEG data.</param>
    /// <param name="colorTransform">The <c>/ColorTransform</c> decode parameter, or <see cref="JpegInfo.NoColorTransform"/>.</param>
    /// <returns>The image, <see langword="null"/> when it is too large, or an image marked unsupported when no decoder can read it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? Decode(in ImageHeader header, ReadOnlySpan<byte> data, int colorTransform) =>
        Decode(header, data, colorTransform, null, 0);

    /// <summary>Decodes a JPEG image, reporting data that ends before its end-of-image marker.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JPEG data.</param>
    /// <param name="colorTransform">The <c>/ColorTransform</c> decode parameter, or <see cref="JpegInfo.NoColorTransform"/>.</param>
    /// <param name="context">The open context that receives the report, or <see langword="null"/>.</param>
    /// <param name="objectNumber">The image stream's object number, or 0.</param>
    /// <returns>The image, <see langword="null"/> when it is too large, or an image marked unsupported when no decoder can read it.</returns>
    internal static PdfImageData? Decode(in ImageHeader header, ReadOnlySpan<byte> data, int colorTransform, PdfOpenContext? context, int objectNumber)
    {
        ReportIfTruncated(data, context, objectNumber);

        if (!JpegMarkers.TryReadInfo(data, out var info) || !info.IsSupported)
        {
            return Unsupported(header);
        }

        if ((long)info.Width * info.Height > ImageHeader.MaxPixels)
        {
            return null;
        }

        var convert = info.UsesColorTransform(colorTransform);
        var needsRawSamples = info.Components == CmykComponents || (info.Components == RgbComponents && !convert);
        var image = needsRawSamples
            ? DecodeManaged(header, data, info, convert)
            : DecodeSkia(header, data, info) ?? DecodeManaged(header, data, info, convert);
        return image ?? Unsupported(header);
    }

    /// <summary>Reports JPEG data that ends before its end-of-image marker.</summary>
    /// <param name="data">The JPEG data.</param>
    /// <param name="context">The open context, or <see langword="null"/>.</param>
    /// <param name="objectNumber">The image stream's object number, or 0.</param>
    private static void ReportIfTruncated(ReadOnlySpan<byte> data, PdfOpenContext? context, int objectNumber)
    {
        // A cut-off scan leaves no end marker, so one tail check covers both decoders. The context keeps each report once per stream.
        if (context is not null && !JpegMarkers.EndsWithEndOfImage(data))
        {
            PdfOpenContext.Report(context, PdfDiagnosticCode.TruncatedStream, TruncatedMessage, objectNumber, -1);
        }
    }

    /// <summary>Creates the result for a JPEG that no decoder can read.</summary>
    /// <param name="header">The image header.</param>
    /// <returns>An image with no pixels that names the JPEG codec.</returns>
    private static PdfImageData Unsupported(in ImageHeader header) =>
        new(header.Width, header.Height, [], header.IsStencil, header.Interpolate, PdfImageCodec.Jpeg);

    /// <summary>Determines whether a decode produced usable pixels; truncated data still gives the rows it had.</summary>
    /// <param name="result">The codec result.</param>
    /// <returns><see langword="true"/> when the pixels can be used.</returns>
    private static bool Succeeded(SKCodecResult result) => result is SKCodecResult.Success or SKCodecResult.IncompleteInput;

    /// <summary>Gets the header an 8-bit JPEG decodes with: the JPEG's size, and a colour space that has its component count.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="info">The JPEG's headers.</param>
    /// <returns>The header for <see cref="PdfImageDecoder.DecodeSamples"/>.</returns>
    private static ImageHeader ForSamples(in ImageHeader header, in JpegInfo info)
    {
        var space = header.ColorSpace;
        var matches = space is not null && space.Components == info.Components;
        return header with
        {
            Width = info.Width,
            Height = info.Height,
            BitsPerComponent = SampleBits,
            IsStencil = false,
            ColorSpace = matches ? space : PdfColorSpace.FromComponents(info.Components),
            Decode = matches ? header.Decode : null,
        };
    }

    /// <summary>Decodes with the managed decoder and runs the raw samples through the colour space.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JPEG data.</param>
    /// <param name="info">The JPEG's headers.</param>
    /// <param name="convert">Whether the samples are YCbCr or YCCK and need converting.</param>
    /// <returns>The image, or <see langword="null"/> when the JPEG cannot be decoded.</returns>
    private static PdfImageData? DecodeManaged(in ImageHeader header, ReadOnlySpan<byte> data, in JpegInfo info, bool convert)
    {
        var length = info.Width * info.Height * info.Components;
        var samples = ScratchPool<byte>.Shared.Rent(length);
        try
        {
            var raw = samples.AsSpan(0, length);
            return JpegDecoder.TryDecode(data, convert, raw) ? PdfImageDecoder.DecodeSamples(ForSamples(header, info), raw) : null;
        }
        finally
        {
            ScratchPool<byte>.Shared.Return(samples);
        }
    }

    /// <summary>Decodes with SkiaSharp, reading the span in place.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JPEG data.</param>
    /// <param name="info">The JPEG's headers.</param>
    /// <returns>The image, or <see langword="null"/> when SkiaSharp cannot decode it.</returns>
    private static unsafe PdfImageData? DecodeSkia(in ImageHeader header, ReadOnlySpan<byte> data, in JpegInfo info)
    {
        // The pointer stays valid while the codec reads: everything that uses it is disposed inside the fixed block.
        fixed (byte* pointer = data)
        {
            using var encoded = SKData.Create((IntPtr)pointer, data.Length, null);
            using var stream = new SKMemoryStream(encoded);
            using var codec = SKCodec.Create(stream, out var created);
            return created == SKCodecResult.Success ? DecodeCodec(codec, header, info) : null;
        }
    }

    /// <summary>Decodes an open SkiaSharp codec to pixels.</summary>
    /// <param name="codec">The codec.</param>
    /// <param name="header">The image header.</param>
    /// <param name="info">The JPEG's headers.</param>
    /// <returns>The image, or <see langword="null"/>.</returns>
    private static PdfImageData? DecodeCodec(SKCodec codec, in ImageHeader header, in JpegInfo info)
    {
        var size = codec.Info;
        if (size.Width != info.Width || size.Height != info.Height)
        {
            return null;
        }

        var jpeg = ForSamples(header, info);
        if (info.Components == 1)
        {
            return DecodeGray(codec, jpeg);
        }

        var pixels = PixelMemory.Allocate((long)size.Width * size.Height * PixelConverter.BytesPerPixel, out var pinned);
        if (!Succeeded(codec.GetPixels(new(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Opaque), pixels)))
        {
            return null;
        }

        Recolor(header with { Width = info.Width, Height = info.Height }, pixels);
        return new(size.Width, size.Height, pixels, false, header.Interpolate, PdfImageCodec.None) { IsPinned = pinned };
    }

    /// <summary>Decodes a gray JPEG and converts it like an 8-bit one-component image.</summary>
    /// <param name="codec">The codec.</param>
    /// <param name="header">The image header with the JPEG's size and a one-component colour space.</param>
    /// <returns>The image, or <see langword="null"/>.</returns>
    private static PdfImageData? DecodeGray(SKCodec codec, in ImageHeader header)
    {
        var length = header.Width * header.Height;
        var gray = ScratchPool<byte>.Shared.Rent(length);
        try
        {
            var result = codec.GetPixels(new(header.Width, header.Height, SKColorType.Gray8, SKAlphaType.Opaque), gray);
            return Succeeded(result) ? PdfImageDecoder.DecodeSamples(header, gray.AsSpan(0, length)) : null;
        }
        finally
        {
            ScratchPool<byte>.Shared.Return(gray);
        }
    }

    /// <summary>Applies a /Decode array or a non-RGB three-component colour space to SkiaSharp's RGB output.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="pixels">The BGRA pixels, converted in place.</param>
    private static void Recolor(in ImageHeader header, byte[] pixels)
    {
        var space = header.ColorSpace;
        if (space is null || space.Components != RgbComponents || (header.Decode is null && space.Kind == PdfColorSpaceKind.DeviceRgb))
        {
            return;
        }

        var unpacker = new SampleUnpacker(SampleBits, RgbComponents, header.GetDecode(), space, null);
        var samples = ScratchPool<byte>.Shared.Rent(header.Width * RgbComponents);
        try
        {
            for (var y = 0; y < header.Height; y++)
            {
                var row = pixels.AsSpan(y * header.Width * PixelConverter.BytesPerPixel, header.Width * PixelConverter.BytesPerPixel);
                for (var x = 0; x < header.Width; x++)
                {
                    var pixel = row.Slice(x * PixelConverter.BytesPerPixel, PixelConverter.BytesPerPixel);
                    var sample = samples.AsSpan(x * RgbComponents, RgbComponents);
                    sample[0] = unpacker.Map(0, pixel[RedIndex]);
                    sample[1] = unpacker.Map(1, pixel[1]);
                    sample[RedIndex] = unpacker.Map(RedIndex, pixel[0]);
                }

                space.ConvertRow(samples, row, header.Width);
            }
        }
        finally
        {
            ScratchPool<byte>.Shared.Return(samples);
        }
    }
}
