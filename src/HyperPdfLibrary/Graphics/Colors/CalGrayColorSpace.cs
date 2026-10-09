// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// The CalGray colour space. Like PDFium's CPDF_CalGray, the gray value passes through unchanged: the /Gamma, white point
/// and black point are ignored, which keeps the result identical to PDFium's.
/// </summary>
internal sealed class CalGrayColorSpace : PdfColorSpace
{
    /// <summary>Initializes a new instance of the <see cref="CalGrayColorSpace"/> class.</summary>
    internal CalGrayColorSpace()
    {
    }

    /// <inheritdoc/>
    public override int Components => 1;

    /// <inheritdoc/>
    public override PdfColorSpaceKind Kind => PdfColorSpaceKind.CalGray;

    /// <inheritdoc/>
    private protected override void ToRgbCore(ReadOnlySpan<float> components, Span<float> rgb) =>
        rgb[..RgbComponents].Fill(Math.Clamp(components[0], 0, 1));

    /// <inheritdoc/>
    private protected override void ConvertRowCore(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount) =>
        PixelConverter.GrayToBgra(samples, bgra, pixelCount);
}
