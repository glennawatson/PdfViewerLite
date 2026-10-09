// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Fonts;

/// <summary>A PostScript font name split into its family and the style it states, such as <c>Arial,BoldItalic</c>.</summary>
/// <param name="Family">The family part.</param>
/// <param name="Weight">The weight the style states, or zero when it states none.</param>
/// <param name="Italic">Whether the style says italic or oblique.</param>
[DebuggerDisplay("ParsedFontName: {Family} {Weight} {Italic}")]
internal readonly record struct ParsedFontName(string Family, int Weight, bool Italic)
{
    /// <summary>The weight of a light face.</summary>
    internal const int LightWeight = 300;

    /// <summary>The weight of a regular face.</summary>
    internal const int NormalWeight = 400;

    /// <summary>The weight of a medium face.</summary>
    internal const int MediumWeight = 500;

    /// <summary>The weight of a semibold face.</summary>
    internal const int SemiBoldWeight = 600;

    /// <summary>The weight of a bold face.</summary>
    internal const int BoldWeight = 700;

    /// <summary>The weight of an extra bold face.</summary>
    internal const int ExtraBoldWeight = 800;

    /// <summary>The weight of a black face.</summary>
    internal const int BlackWeight = 900;

    /// <summary>The share of a family's length added for the spaces between its words.</summary>
    private const int SpaceShare = 2;

    /// <summary>The style words that state a weight, checked in order so the longer words win.</summary>
    private static readonly string[] WeightWords = ["Black", "Heavy", "ExtraBold", "UltraBold", "SemiBold", "DemiBold", "Demi", "Bold", "Medium", "Light"];

    /// <summary>The weight of each word in <see cref="WeightWords"/>.</summary>
    private static readonly int[] WeightValues =
        [BlackWeight, BlackWeight, ExtraBoldWeight, ExtraBoldWeight, SemiBoldWeight, SemiBoldWeight, SemiBoldWeight, BoldWeight, MediumWeight, LightWeight];

    /// <summary>Splits a font name. A comma, then the last hyphen, separates the style; else a style word ending the name is taken.</summary>
    /// <param name="name">The name without a subset tag.</param>
    /// <returns>The parts.</returns>
    internal static ParsedFontName Parse(string name)
    {
        var comma = name.IndexOf(',', StringComparison.Ordinal);
        var hyphen = name.LastIndexOf('-');
        var split = comma >= 0 ? comma : hyphen;
        if (split > 0)
        {
            var style = name[(split + 1)..];
            return new(name[..split], WeightOf(style), IsItalic(style));
        }

        foreach (var suffix in (ReadOnlySpan<string>)["BoldItalic", "BoldOblique", "Bold", "Italic", "Oblique"])
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
            {
                return new(name[..^suffix.Length], WeightOf(suffix), IsItalic(suffix));
            }
        }

        return new(name, 0, false);
    }

    /// <summary>Turns a PostScript family such as <c>TimesNewRomanPSMT</c> into words such as <c>Times New Roman</c>.</summary>
    /// <param name="family">The family.</param>
    /// <returns>The spaced family, or the family unchanged when it has no inner capitals.</returns>
    internal static string ToWords(string family)
    {
        var trimmed = family;
        foreach (var suffix in (ReadOnlySpan<string>)["PSMT", "MT", "PS"])
        {
            if (trimmed.Length <= suffix.Length || !trimmed.EndsWith(suffix, StringComparison.Ordinal))
            {
                continue;
            }

            trimmed = trimmed[..^suffix.Length];
            break;
        }

        var words = new StringBuilder(trimmed.Length + (trimmed.Length / SpaceShare));
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(trimmed[i - 1]))
            {
                _ = words.Append(' ');
            }

            _ = words.Append(c);
        }

        return words.ToString();
    }

    /// <summary>Reads the weight a style word states.</summary>
    /// <param name="style">The style.</param>
    /// <returns>The weight, or zero when none is stated.</returns>
    private static int WeightOf(string style)
    {
        var words = WeightWords;
        for (var i = 0; i < words.Length; i++)
        {
            if (Has(style, words[i]))
            {
                return WeightValues[i];
            }
        }

        return 0;
    }

    /// <summary>Determines whether a style says italic.</summary>
    /// <param name="style">The style.</param>
    /// <returns><see langword="true"/> for italic or oblique.</returns>
    private static bool IsItalic(string style) => Has(style, "Italic") || Has(style, "Oblique");

    /// <summary>Determines whether a style holds a word, ignoring case.</summary>
    /// <param name="style">The style.</param>
    /// <param name="word">The word.</param>
    /// <returns><see langword="true"/> when found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Has(string style, string word) => style.Contains(word, StringComparison.OrdinalIgnoreCase);
}
