// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// Decodes a JBIG2Decode image for <see cref="PdfImageDecoder"/>: the page becomes 1-bit samples, 0 for black, which
/// then go through the image's colour space and /Decode array like any 1-bit image, or become a stencil mask.
/// </summary>
internal static class Jbig2ImageDecoder
{
    /// <summary>The bits of a JBIG2 sample.</summary>
    private const int OneBit = 1;

    /// <summary>Decodes a JBIG2 image.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JBIG2 data after the stream's other filters.</param>
    /// <param name="parameters">The JBIG2Decode parameters, or <see langword="null"/>; /JBIG2Globals is read from them.</param>
    /// <returns>The image, or <see langword="null"/> when no page could be decoded.</returns>
    /// <exception cref="InvalidDataException">The /JBIG2Globals stream's filters fail.</exception>
    internal static PdfImageData? Decode(in ImageHeader header, ReadOnlySpan<byte> data, PdfDictionary? parameters)
    {
        var bits = header with { BitsPerComponent = OneBit };
        if (bits.ColorSpace?.Components > 1)
        {
            bits = bits with { ColorSpace = PdfColorSpace.DeviceGray, Decode = null };
        }

        var length = bits.RowBytes * bits.Height;
        var rows = ScratchPool<byte>.Shared.Rent(length);
        var globals = default(PooledBuffer);
        try
        {
            _ = parameters?.GetStream(KnownName.JBIG2Globals)?.Decode(ref globals);
            var output = rows.AsSpan(0, length);
            return Jbig2Decoder.TryDecode(data, globals.WrittenSpan, bits.Width, bits.Height, output) ? PdfImageDecoder.DecodeSamples(bits, output) : null;
        }
        finally
        {
            globals.Dispose();
            ScratchPool<byte>.Shared.Return(rows);
        }
    }
}
