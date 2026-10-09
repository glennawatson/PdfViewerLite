// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// The CIE L*a*b* colour space, converted to sRGB. XYZ is built from the dictionary's /WhitePoint and converted with the
/// same white-point-aware sRGB matrix as CalRGB, so the white point maps to white. A row converts through a cache of
/// earlier conversions, because the maths needs cube roots and powers.
/// </summary>
internal sealed class LabColorSpace : PdfColorSpace
{
    /// <summary>The largest L* value.</summary>
    private const float MaxLightness = 100;

    /// <summary>The default a* and b* limit.</summary>
    private const float DefaultChromaLimit = 100;

    /// <summary>The L* offset in the CIE formula.</summary>
    private const float LightnessOffset = 16;

    /// <summary>The L* divisor in the CIE formula.</summary>
    private const float LightnessScale = 116;

    /// <summary>The a* divisor in the CIE formula.</summary>
    private const float AScale = 500;

    /// <summary>The b* divisor in the CIE formula.</summary>
    private const float BScale = 200;

    /// <summary>The CIE threshold 6/29.</summary>
    private const float Epsilon = 6F / 29F;

    /// <summary>The slope of the linear segment, 3 (6/29)^2.</summary>
    private const float LinearSlope = 108F / 841F;

    /// <summary>The offset of the linear segment, 4/29.</summary>
    private const float LinearOffset = 4F / 29F;

    /// <summary>The length of the a* and b* range array.</summary>
    private const int RangeLength = 4;

    /// <summary>The a* and b* ranges: amin, amax, bmin, bmax.</summary>
    private readonly float[] _range;

    /// <summary>The XYZ to sRGB conversion for the white point.</summary>
    private readonly WhitePointConverter _converter;

    /// <summary>The cache of earlier row conversions.</summary>
    private readonly ColorCache _cache = new();

    /// <summary>Initializes a new instance of the <see cref="LabColorSpace"/> class.</summary>
    /// <param name="range">The a* and b* ranges.</param>
    /// <param name="converter">The XYZ to sRGB conversion, which also holds the white point.</param>
    internal LabColorSpace(float[] range, WhitePointConverter converter)
    {
        _range = range;
        _converter = converter;
    }

    /// <inheritdoc/>
    public override int Components => RgbComponents;

    /// <inheritdoc/>
    public override PdfColorSpaceKind Kind => PdfColorSpaceKind.Lab;

    /// <inheritdoc/>
    public override void GetInitialColor(Span<float> components)
    {
        components[0] = 0;
        components[1] = Math.Clamp(0, _range[0], _range[1]);
        components[2] = Math.Clamp(0, _range[2], _range[RangeLength - 1]);
    }

    /// <inheritdoc/>
    public override float[] GetDefaultDecode(int bitsPerComponent) => [0, MaxLightness, _range[0], _range[1], _range[2], _range[RangeLength - 1]];

    /// <summary>Parses a Lab dictionary.</summary>
    /// <param name="dictionary">The dictionary, or <see langword="null"/>.</param>
    /// <returns>The colour space.</returns>
    internal static LabColorSpace Parse(PdfDictionary? dictionary)
    {
        float[] range = [-DefaultChromaLimit, DefaultChromaLimit, -DefaultChromaLimit, DefaultChromaLimit];
        var array = dictionary?.GetArray(KnownName.Range);
        Span<float> read = stackalloc float[RangeLength];
        if (array?.ReadNumbers(read) == RangeLength && read[0] <= read[1] && read[2] <= read[RangeLength - 1])
        {
            read.CopyTo(range);
        }

        return new(range, WhitePointConverter.FromDictionary(dictionary));
    }

    /// <inheritdoc/>
    private protected override void ToRgbCore(ReadOnlySpan<float> components, Span<float> rgb)
    {
        var lightness = Math.Clamp(components[0], 0, MaxLightness);
        var a = Math.Clamp(components[1], _range[0], _range[1]);
        var b = Math.Clamp(components[2], _range[2], _range[RangeLength - 1]);
        var fy = (lightness + LightnessOffset) / LightnessScale;
        var x = Inverse(fy + (a / AScale)) * _converter.WhiteX;
        var y = Inverse(fy);
        var z = Inverse(fy - (b / BScale)) * _converter.WhiteZ;
        _converter.ToRgb(x, y, z, rgb);
    }

    /// <inheritdoc/>
    private protected override void ConvertRowCore(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount) =>
        ConvertRowScalar(samples, bgra, pixelCount, _cache);

    /// <summary>The inverse of the CIE f function.</summary>
    /// <param name="value">The f value.</param>
    /// <returns>The normalised tristimulus value.</returns>
    private static float Inverse(float value) => value >= Epsilon ? value * value * value : LinearSlope * (value - LinearOffset);
}
