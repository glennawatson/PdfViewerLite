// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Redaction;

/// <summary>Reads and writes the <c>/DA</c> string of a redact annotation: a font and size, then a colour.</summary>
internal static class DefaultAppearance
{
    /// <summary>The components of a grey colour.</summary>
    private const int GrayComponents = 1;

    /// <summary>The components of an RGB colour.</summary>
    private const int RgbComponents = 3;

    /// <summary>The components of a CMYK colour.</summary>
    private const int CmykComponents = 4;

    /// <summary>The bits the red channel is shifted left by.</summary>
    private const int RedShift = 16;

    /// <summary>The bits the green channel is shifted left by.</summary>
    private const int GreenShift = 8;

    /// <summary>The highest channel value as a float.</summary>
    private const float ChannelMax = 255F;

    /// <summary>The mask of one channel.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>The index of the second component of a colour.</summary>
    private const int SecondComponent = 2;

    /// <summary>The index of the third component of a colour.</summary>
    private const int ThirdComponent = 3;

    /// <summary>The characters that separate the tokens of a default appearance.</summary>
    private static readonly char[] Separators = [' ', '\t', '\r', '\n'];

    /// <summary>Reads the font size and text colour.</summary>
    /// <param name="text">The <c>/DA</c> string, possibly empty.</param>
    /// <param name="size">Receives the font size; 0 when the string gives none or asks for automatic sizing.</param>
    /// <param name="color">Receives the text colour as 0xRRGGBB, or <see langword="null"/> when the string sets none.</param>
    internal static void Read(string text, out float size, out uint? color)
    {
        size = 0;
        color = null;
        var tokens = text.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < tokens.Length; i++)
        {
            if (tokens[i] == "Tf" && i >= SecondComponent && float.TryParse(tokens[i - 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                size = Math.Max(0, parsed);
            }
            else if (tokens[i] is "g" or "rg" or "k")
            {
                color = ReadColor(tokens, i);
            }
        }
    }

    /// <summary>Writes a default appearance with Helvetica.</summary>
    /// <param name="size">The font size in points.</param>
    /// <param name="color">The text colour as 0xRRGGBB, or <see langword="null"/> for black.</param>
    /// <returns>The string.</returns>
    internal static string Write(float size, uint? color)
    {
        var colour = "0 g";
        if (color is { } rgb)
        {
            var red = ((rgb >> RedShift) & ChannelMask) / ChannelMax;
            var green = ((rgb >> GreenShift) & ChannelMask) / ChannelMax;
            colour = string.Create(CultureInfo.InvariantCulture, $"{red:0.###} {green:0.###} {(rgb & ChannelMask) / ChannelMax:0.###} rg");
        }

        return string.Create(CultureInfo.InvariantCulture, $"/Helv {size:0.###} Tf {colour}");
    }

    /// <summary>Reads the colour a colour operator sets.</summary>
    /// <param name="tokens">The tokens.</param>
    /// <param name="at">The index of the operator.</param>
    /// <returns>The colour as 0xRRGGBB, or <see langword="null"/> when the operands are missing.</returns>
    private static uint? ReadColor(string[] tokens, int at)
    {
        var count = tokens[at] switch
        {
            "g" => GrayComponents,
            "rg" => RgbComponents,
            _ => CmykComponents,
        };
        if (at < count)
        {
            return null;
        }

        var values = new float[count];
        for (var i = 0; i < count; i++)
        {
            if (!float.TryParse(tokens[at - count + i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
            {
                return null;
            }
        }

        return count switch
        {
            GrayComponents => Pack(values[0], values[0], values[0]),
            RgbComponents => Pack(values[0], values[1], values[SecondComponent]),
            _ => Pack((1 - values[0]) * (1 - values[ThirdComponent]), (1 - values[1]) * (1 - values[ThirdComponent]), (1 - values[SecondComponent]) * (1 - values[ThirdComponent])),
        };
    }

    /// <summary>Packs three levels into 0xRRGGBB.</summary>
    /// <param name="red">The red level from 0 to 1.</param>
    /// <param name="green">The green level from 0 to 1.</param>
    /// <param name="blue">The blue level from 0 to 1.</param>
    /// <returns>The packed colour.</returns>
    private static uint Pack(float red, float green, float blue) =>
        ((uint)MathF.Round(Math.Clamp(red, 0, 1) * ChannelMax) << RedShift)
        | ((uint)MathF.Round(Math.Clamp(green, 0, 1) * ChannelMax) << GreenShift)
        | (uint)MathF.Round(Math.Clamp(blue, 0, 1) * ChannelMax);
}
