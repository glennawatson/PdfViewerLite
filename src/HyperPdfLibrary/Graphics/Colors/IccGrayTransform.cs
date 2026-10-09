// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// A gray ICC profile with a /kTRC curve. The curve gives the luminance, whose D50 white maps to sRGB white, so the result
/// is the luminance encoded with the sRGB curve. A 256-entry table converts rows.
/// </summary>
internal sealed class IccGrayTransform : IccTransform
{
    /// <summary>The curve from gray to linear luminance.</summary>
    private readonly IccCurve _curve;

    /// <summary>The BGRA word for every 8-bit gray, in native byte order.</summary>
    private readonly uint[] _lookup = new uint[PixelConverter.LookupSize];

    /// <summary>Initializes a new instance of the <see cref="IccGrayTransform"/> class.</summary>
    /// <param name="curve">The gray tone curve.</param>
    internal IccGrayTransform(IccCurve curve)
    {
        _curve = curve;
        Span<float> rgb = stackalloc float[PdfColorSpace.RgbComponents];
        for (var i = 0; i < _lookup.Length; i++)
        {
            ToRgb([i / (float)PixelConverter.MaxByte], rgb);
            _lookup[i] = PixelConverter.PackNative(rgb);
        }
    }

    /// <inheritdoc/>
    internal override int Components => 1;

    /// <inheritdoc/>
    internal override void ToRgb(ReadOnlySpan<float> components, Span<float> rgb) =>
        rgb[..PdfColorSpace.RgbComponents].Fill(SrgbTransfer.Encode(_curve.Evaluate(components[0])));

    /// <inheritdoc/>
    internal override void ConvertRow(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount) =>
        PixelConverter.LookupToBgra(samples, bgra, pixelCount, _lookup);
}
