// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>
/// Reads glyph names that only carry a number, which subsetting tools write when the font has no real names:
/// <c>g23</c>, <c>gid23</c>, <c>glyph23</c> and <c>index23</c> give a glyph id; <c>cid23</c> gives a CID.
/// </summary>
internal static class NumberedGlyphName
{
    /// <summary>The most digits a glyph number has.</summary>
    private const int MaxDigits = 5;

    /// <summary>The base of decimal digits.</summary>
    private const int Ten = 10;

    /// <summary>Reads a numbered glyph name.</summary>
    /// <param name="name">The name's bytes.</param>
    /// <param name="isCid">Whether the number is a CID rather than a glyph id.</param>
    /// <param name="number">The number.</param>
    /// <returns><see langword="true"/> when the name is a prefix and decimal digits only.</returns>
    internal static bool TryParse(ReadOnlySpan<byte> name, out bool isCid, out int number)
    {
        isCid = name.StartsWith("cid"u8);
        return TryDigits(isCid ? name["cid"u8.Length..] : StripPrefix(name), out number);
    }

    /// <summary>Removes a glyph-id prefix, trying the longest first so <c>glyph23</c> and <c>gid23</c> are not read as <c>g</c> followed by letters.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The bytes after the prefix, or empty when the name has none.</returns>
    private static ReadOnlySpan<byte> StripPrefix(ReadOnlySpan<byte> name)
    {
        if (name.StartsWith("glyph"u8))
        {
            return name["glyph"u8.Length..];
        }

        if (name.StartsWith("index"u8))
        {
            return name["index"u8.Length..];
        }

        if (name.StartsWith("gid"u8))
        {
            return name["gid"u8.Length..];
        }

        return name.StartsWith("g"u8) ? name[1..] : [];
    }

    /// <summary>Reads one to five decimal digits.</summary>
    /// <param name="digits">The bytes.</param>
    /// <param name="number">The number.</param>
    /// <returns><see langword="true"/> when the bytes are only digits.</returns>
    private static bool TryDigits(ReadOnlySpan<byte> digits, out int number)
    {
        number = 0;
        if (digits.IsEmpty || digits.Length > MaxDigits)
        {
            return false;
        }

        foreach (var digit in digits)
        {
            if (digit is < (byte)'0' or > (byte)'9')
            {
                return false;
            }

            number = (number * Ten) + (digit - '0');
        }

        return true;
    }
}
