// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>Converts the values a profile stores in its connection space to D50 XYZ, and back to Lab for black point detection.</summary>
internal static class IccPcs
{
    /// <summary>The D50 white point X.</summary>
    internal const float WhiteX = 0.9642F;

    /// <summary>The D50 white point Y.</summary>
    internal const float WhiteY = 1F;

    /// <summary>The D50 white point Z.</summary>
    internal const float WhiteZ = 0.8249F;

    /// <summary>The XYZ value that a stored 1.0 stands for.</summary>
    private const float XyzScale = 1F + (32767F / 32768F);

    /// <summary>The L* of a stored 1.0 in a v4 or 8-bit table.</summary>
    private const float LabLightnessRange = 100F;

    /// <summary>The range of a* and b* in a v4 or 8-bit table.</summary>
    private const float LabChromaRange = 255F;

    /// <summary>The a* and b* offset of the stored zero.</summary>
    private const float LabChromaOffset = 128F;

    /// <summary>The stored 16-bit value that stands for an L* of 100.</summary>
    private const float LegacyLightnessMaximum = 65280F;

    /// <summary>The largest 16-bit stored value.</summary>
    private const float Maximum16 = 65535F;

    /// <summary>The 16-bit a* and b* step of one unit.</summary>
    private const float LegacyChromaStep = 256F;

    /// <summary>The divisor of L* plus 16 in the Lab to XYZ conversion.</summary>
    private const float LightnessDivisor = 116F;

    /// <summary>The offset added to L* in the Lab to XYZ conversion.</summary>
    private const float LightnessOffset = 16F;

    /// <summary>The divisor of a* in the Lab to XYZ conversion.</summary>
    private const float AlphaDivisor = 500F;

    /// <summary>The divisor of b* in the Lab to XYZ conversion.</summary>
    private const float BetaDivisor = 200F;

    /// <summary>The cube of the point where the Lab function turns linear, (6/29)^3.</summary>
    private const float LabEpsilon = 216F / 24389F;

    /// <summary>The slope of the linear part of the Lab function, 24389/27 divided by 116.</summary>
    private const float LabKappa = 24389F / 27F;

    /// <summary>The point where the Lab function turns linear, 6/29.</summary>
    private const float LabBreak = 6F / 29F;

    /// <summary>The slope of the linear part of the inverse Lab function, 108/841.</summary>
    private const float LabLinearSlope = 108F / 841F;

    /// <summary>The offset of the linear part of the inverse Lab function, 4/29.</summary>
    private const float LabLinearOffset = 4F / 29F;

    /// <summary>Converts a stored connection space value to D50 XYZ.</summary>
    /// <param name="encoding">How the profile stores the value.</param>
    /// <param name="v0">The first stored value, from 0 to 1.</param>
    /// <param name="v1">The second stored value.</param>
    /// <param name="v2">The third stored value.</param>
    /// <returns>The XYZ colour scaled so that the D50 white has a Y of 1.</returns>
    internal static Float3 ToXyz(IccPcsEncoding encoding, float v0, float v1, float v2) => encoding switch
    {
        IccPcsEncoding.Lab => LabToXyz(v0 * LabLightnessRange, (v1 * LabChromaRange) - LabChromaOffset, (v2 * LabChromaRange) - LabChromaOffset),
        IccPcsEncoding.LabLegacy16 => LabToXyz(
            v0 * Maximum16 / LegacyLightnessMaximum * LabLightnessRange,
            (v1 * Maximum16 / LegacyChromaStep) - LabChromaOffset,
            (v2 * Maximum16 / LegacyChromaStep) - LabChromaOffset),
        _ => new(v0 * XyzScale, v1 * XyzScale, v2 * XyzScale),
    };

    /// <summary>Converts CIE Lab to D50 XYZ.</summary>
    /// <param name="lightness">L*.</param>
    /// <param name="alpha">a*.</param>
    /// <param name="beta">b*.</param>
    /// <returns>The XYZ colour.</returns>
    internal static Float3 LabToXyz(float lightness, float alpha, float beta)
    {
        var fy = (lightness + LightnessOffset) / LightnessDivisor;
        var fx = fy + (alpha / AlphaDivisor);
        var fz = fy - (beta / BetaDivisor);
        return new(WhiteX * Inverse(fx), WhiteY * Inverse(fy), WhiteZ * Inverse(fz));
    }

    /// <summary>Gets L* of an XYZ colour, which black point detection uses.</summary>
    /// <param name="y">The Y of the colour.</param>
    /// <returns>L*.</returns>
    internal static float Lightness(float y)
    {
        var ratio = y / WhiteY;
        var f = ratio > LabEpsilon ? MathF.Cbrt(ratio) : ((LabKappa * ratio / LightnessDivisor) + LabLinearOffset);
        return (LightnessDivisor * f) - LightnessOffset;
    }

    /// <summary>Applies the inverse of the Lab function to one coordinate.</summary>
    /// <param name="f">The coordinate.</param>
    /// <returns>The relative XYZ value.</returns>
    private static float Inverse(float f) => f > LabBreak ? f * f * f : LabLinearSlope * (f - LabLinearOffset);
}
