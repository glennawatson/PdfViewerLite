// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>The sRGB transfer function.</summary>
internal static class SrgbTransfer
{
    /// <summary>The linear value below which the curve is a straight line.</summary>
    private const float LinearLimit = 0.0031308F;

    /// <summary>The slope of the straight segment.</summary>
    private const float LinearSlope = 12.92F;

    /// <summary>The scale of the power segment.</summary>
    private const float PowerScale = 1.055F;

    /// <summary>The offset of the power segment.</summary>
    private const float PowerOffset = 0.055F;

    /// <summary>The exponent of the power segment, 1 / 2.4.</summary>
    private const float Exponent = 1F / 2.4F;

    /// <summary>Encodes a linear value without clamping: negative values mirror the positive curve and values above one continue it.</summary>
    /// <param name="linear">The linear value.</param>
    /// <returns>The encoded value, which is zero for zero and passes through zero smoothly.</returns>
    internal static float EncodeExtended(float linear)
    {
        var magnitude = MathF.Abs(linear);
        var encoded = magnitude > LinearLimit ? (PowerScale * MathF.Pow(magnitude, Exponent)) - PowerOffset : magnitude * LinearSlope;
        return linear < 0 ? -encoded : encoded;
    }

    /// <summary>Encodes a linear value, clamped to 0..1.</summary>
    /// <param name="linear">The linear value.</param>
    /// <returns>The encoded value from 0 to 1.</returns>
    internal static float Encode(float linear)
    {
        if (!(linear > LinearLimit))
        {
            return linear > 0 ? linear * LinearSlope : 0;
        }

        return linear >= 1 ? 1 : (PowerScale * MathF.Pow(linear, Exponent)) - PowerOffset;
    }
}
