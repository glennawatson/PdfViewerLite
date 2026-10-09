// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Fonts.Data;

/// <summary>
/// Maps glyph names to Unicode text by the Adobe Glyph List rules: known names, <c>uniXXXX</c> sequences,
/// <c>uXXXX</c> to <c>uXXXXXX</c> code points, <c>.suffix</c> variants and <c>_</c> ligatures. Also finds the preferred
/// glyph name for a code point. Nothing allocates.
/// </summary>
public static class GlyphList
{
    /// <summary>The hexadecimal digits in one <c>uni</c> group.</summary>
    private const int UniGroupDigits = 4;

    /// <summary>The fewest hexadecimal digits after <c>u</c>.</summary>
    private const int MinUDigits = 4;

    /// <summary>The most hexadecimal digits after <c>u</c>.</summary>
    private const int MaxUDigits = 6;

    /// <summary>The bits in one hexadecimal digit.</summary>
    private const int HexDigitBits = 4;

    /// <summary>The first surrogate code point.</summary>
    private const int SurrogateStart = 0xD800;

    /// <summary>The last surrogate code point.</summary>
    private const int SurrogateEnd = 0xDFFF;

    /// <summary>The largest Unicode scalar value.</summary>
    private const int MaxCodePoint = 0x10FFFF;

    /// <summary>The largest code point that fits one UTF-16 unit.</summary>
    private const int MaxBmp = 0xFFFF;

    /// <summary>The space a single-code-point lookup decodes into.</summary>
    private const int ScratchLength = 16;

    /// <summary>Maps a glyph name to Unicode text.</summary>
    /// <param name="name">The glyph name's bytes.</param>
    /// <param name="destination">Receives the UTF-16 text.</param>
    /// <param name="charsWritten">The number of UTF-16 units written.</param>
    /// <returns><see langword="true"/> when the name maps to some text and it fit.</returns>
    public static bool TryGetUnicode(ReadOnlySpan<byte> name, Span<char> destination, out int charsWritten)
    {
        charsWritten = 0;
        var dot = name.IndexOf((byte)'.');
        var stem = dot < 0 ? name : name[..dot];
        while (!stem.IsEmpty)
        {
            var underscore = stem.IndexOf((byte)'_');
            var component = underscore < 0 ? stem : stem[..underscore];
            if (!AppendComponent(component, destination, ref charsWritten))
            {
                charsWritten = 0;
                return false;
            }

            stem = underscore < 0 ? [] : stem[(underscore + 1)..];
        }

        return charsWritten > 0;
    }

    /// <summary>Maps a glyph name to a single code point.</summary>
    /// <param name="name">The glyph name's bytes.</param>
    /// <param name="codePoint">The code point.</param>
    /// <returns><see langword="true"/> when the name maps to exactly one code point.</returns>
    public static bool TryGetCodePoint(ReadOnlySpan<byte> name, out int codePoint)
    {
        Span<char> text = stackalloc char[ScratchLength];
        codePoint = 0;
        if (!TryGetUnicode(name, text, out var written))
        {
            return false;
        }

        var status = Rune.DecodeFromUtf16(text[..written], out var rune, out var consumed);
        if (status != System.Buffers.OperationStatus.Done || consumed != written)
        {
            return false;
        }

        codePoint = rune.Value;
        return true;
    }

    /// <summary>Maps a glyph name from the ITC Zapf Dingbats font to its code point.</summary>
    /// <param name="name">The glyph name's bytes, such as <c>a1</c>.</param>
    /// <param name="codePoint">The code point.</param>
    /// <returns><see langword="true"/> when the name is in the Zapf Dingbats list.</returns>
    public static bool TryGetDingbatsCodePoint(ReadOnlySpan<byte> name, out int codePoint)
    {
        var id = GlyphNames.Find(name);
        var index = id == GlyphNames.NoName ? -1 : GlyphListData.DingbatsNameIds.BinarySearch((ushort)id);
        codePoint = index < 0 ? 0 : GlyphListData.DingbatsCodePoints[index];
        return index >= 0;
    }

    /// <summary>Finds the preferred glyph name for a code point, as the standard fonts name their glyphs.</summary>
    /// <param name="codePoint">The code point.</param>
    /// <param name="name">The name's ASCII bytes.</param>
    /// <returns><see langword="true"/> when the Adobe Glyph List names the code point.</returns>
    public static bool TryGetName(int codePoint, out ReadOnlySpan<byte> name)
    {
        var index = (uint)codePoint > MaxBmp ? -1 : GlyphListData.ReverseCodePoints.BinarySearch((ushort)codePoint);
        name = index < 0 ? [] : GlyphNames.Get(GlyphListData.ReverseNameIds[index]);
        return index >= 0;
    }

    /// <summary>Gets the Adobe Glyph List text for a known name id.</summary>
    /// <param name="id">The name id.</param>
    /// <returns>The UTF-16 text, or empty when the list does not hold the name.</returns>
    internal static ReadOnlySpan<char> GetListedText(int id)
    {
        if (!GlyphNames.IsName(id))
        {
            return [];
        }

        int start = GlyphListData.AglStarts[id];
        int end = GlyphListData.AglStarts[id + 1];
        return MemoryMarshal.Cast<ushort, char>(GlyphListData.AglText[start..end]);
    }

    /// <summary>Appends the text of one name component.</summary>
    /// <param name="component">The component.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="written">The units written so far.</param>
    /// <returns><see langword="false"/> when the destination is too small.</returns>
    private static bool AppendComponent(ReadOnlySpan<byte> component, Span<char> destination, ref int written)
    {
        if (component.IsEmpty)
        {
            return true;
        }

        var listed = GetListedText(GlyphNames.Find(component));
        if (!listed.IsEmpty)
        {
            return Append(listed, destination, ref written);
        }

        // A component that follows none of the rules maps to nothing, which is not an error.
        return TryAppendUni(component, destination, ref written, out var fits)
            ? fits
            : !TryParseU(component, out var codePoint) || AppendCodePoint(codePoint, destination, ref written);
    }

    /// <summary>Appends a <c>uniXXXX[XXXX...]</c> component.</summary>
    /// <param name="component">The component.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="written">The units written so far.</param>
    /// <param name="fits">Set to <see langword="false"/> when the destination is too small.</param>
    /// <returns><see langword="true"/> when the component is a valid <c>uni</c> name.</returns>
    private static bool TryAppendUni(ReadOnlySpan<byte> component, Span<char> destination, ref int written, out bool fits)
    {
        fits = true;
        var digits = component.StartsWith("uni"u8) ? component["uni"u8.Length..] : [];
        if (digits.IsEmpty || digits.Length % UniGroupDigits != 0)
        {
            return false;
        }

        var groups = digits.Length / UniGroupDigits;
        for (var group = 0; group < groups; group++)
        {
            if (!TryParseHex(digits.Slice(group * UniGroupDigits, UniGroupDigits), out var value) || value is >= SurrogateStart and <= SurrogateEnd)
            {
                return false;
            }
        }

        for (var group = 0; group < groups && fits; group++)
        {
            _ = TryParseHex(digits.Slice(group * UniGroupDigits, UniGroupDigits), out var value);
            fits = AppendCodePoint(value, destination, ref written);
        }

        return true;
    }

    /// <summary>Parses a <c>uXXXX</c> to <c>uXXXXXX</c> component.</summary>
    /// <param name="component">The component.</param>
    /// <param name="codePoint">The code point.</param>
    /// <returns><see langword="true"/> when the component is a valid <c>u</c> name.</returns>
    private static bool TryParseU(ReadOnlySpan<byte> component, out int codePoint)
    {
        codePoint = 0;
        return component.Length >= MinUDigits + 1
            && component.Length <= MaxUDigits + 1
            && component[0] == 'u'
            && TryParseHex(component[1..], out codePoint)
            && codePoint <= MaxCodePoint
            && codePoint is < SurrogateStart or > SurrogateEnd;
    }

    /// <summary>Parses hexadecimal digits.</summary>
    /// <param name="digits">The digits.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when every byte is a hexadecimal digit.</returns>
    private static bool TryParseHex(ReadOnlySpan<byte> digits, out int value)
    {
        value = 0;
        foreach (var digit in digits)
        {
            var nibble = PdfCharacters.HexValue(digit);
            if (nibble < 0)
            {
                return false;
            }

            value = (value << HexDigitBits) | nibble;
        }

        return true;
    }

    /// <summary>Appends a code point as UTF-16.</summary>
    /// <param name="codePoint">The code point.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="written">The units written so far.</param>
    /// <returns><see langword="false"/> when the destination is too small.</returns>
    private static bool AppendCodePoint(int codePoint, Span<char> destination, ref int written)
    {
        if (!new Rune(codePoint).TryEncodeToUtf16(destination[written..], out var count))
        {
            return false;
        }

        written += count;
        return true;
    }

    /// <summary>Appends text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="written">The units written so far.</param>
    /// <returns><see langword="false"/> when the destination is too small.</returns>
    private static bool Append(ReadOnlySpan<char> text, Span<char> destination, ref int written)
    {
        if (!text.TryCopyTo(destination[written..]))
        {
            return false;
        }

        written += text.Length;
        return true;
    }
}
