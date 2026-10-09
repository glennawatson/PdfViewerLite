// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// Metrics of the built in Latin fonts, from the Adobe Core 14 font metrics, for laying out and measuring text written
/// in an appearance stream with WinAnsi encoding. Advances and glyph boxes are in ems.
/// </summary>
public static class AppearanceFontMetrics
{
    /// <summary>The first WinAnsi code with metrics: the space.</summary>
    private const int FirstCode = 32;

    /// <summary>The number of codes with metrics in each font.</summary>
    private const int CodeCount = 224;

    /// <summary>The numbers kept for each code: advance, left, bottom, right and top.</summary>
    private const int ValuesPerCode = 5;

    /// <summary>The metric units in an em.</summary>
    private const float UnitsPerEm = 1000;

    /// <summary>The fonts of each family: regular, bold, italic and bold italic.</summary>
    private const int StylesPerFamily = 4;

    /// <summary>The families: Helvetica, Times and Courier.</summary>
    private const int FamilyCount = 3;

    /// <summary>The fonts with metrics.</summary>
    private const int FontCount = FamilyCount * StylesPerFamily;

    /// <summary>The extents kept for each family: ascent and descent.</summary>
    private const int ExtentsPerFamily = 2;

    /// <summary>The first WinAnsi code that differs from Latin-1.</summary>
    private const int WindowsFirst = 0x80;

    /// <summary>The last WinAnsi code that differs from Latin-1.</summary>
    private const int WindowsLast = 0x9F;

    /// <summary>The offset of a code's left edge in its metrics.</summary>
    private const int LeftOffset = 1;

    /// <summary>The offset of a code's bottom edge in its metrics.</summary>
    private const int BottomOffset = 2;

    /// <summary>The offset of a code's right edge in its metrics.</summary>
    private const int RightOffset = 3;

    /// <summary>The offset of a code's top edge in its metrics.</summary>
    private const int TopOffset = 4;

    /// <summary>The bold style's offset within a family.</summary>
    private const int BoldStyle = 1;

    /// <summary>The italic style's offset within a family.</summary>
    private const int ItalicStyle = 2;

    /// <summary>Gets a font in a family and style.</summary>
    /// <param name="family">0 for Helvetica, 1 for Times, 2 for Courier; others read as Helvetica.</param>
    /// <param name="bold">Whether bold.</param>
    /// <param name="italic">Whether italic or oblique.</param>
    /// <returns>The font.</returns>
    public static AppearanceFont FromStyle(int family, bool bold, bool italic)
    {
        var first = (uint)family < FamilyCount ? family * StylesPerFamily : 0;
        return (AppearanceFont)(first + (bold ? BoldStyle : 0) + (italic ? ItalicStyle : 0));
    }

    /// <summary>Gets a font's PDF base font name, such as <c>Times-BoldItalic</c>.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The name's bytes.</returns>
    public static ReadOnlySpan<byte> GetBaseFontName(AppearanceFont font) => font switch
    {
        AppearanceFont.HelveticaBold => "Helvetica-Bold"u8,
        AppearanceFont.HelveticaOblique => "Helvetica-Oblique"u8,
        AppearanceFont.HelveticaBoldOblique => "Helvetica-BoldOblique"u8,
        AppearanceFont.TimesRoman => "Times-Roman"u8,
        AppearanceFont.TimesBold => "Times-Bold"u8,
        AppearanceFont.TimesItalic => "Times-Italic"u8,
        AppearanceFont.TimesBoldItalic => "Times-BoldItalic"u8,
        AppearanceFont.Courier => "Courier"u8,
        AppearanceFont.CourierBold => "Courier-Bold"u8,
        AppearanceFont.CourierOblique => "Courier-Oblique"u8,
        AppearanceFont.CourierBoldOblique => "Courier-BoldOblique"u8,
        _ => "Helvetica"u8,
    };

    /// <summary>Gets how far a font rises above the baseline.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The ascent in ems.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetAscent(AppearanceFont font) => AppearanceFontMetricsData.FamilyExtents[Family(font) * ExtentsPerFamily] / UnitsPerEm;

    /// <summary>Gets how far a font falls below the baseline.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The descent in ems, negative.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetDescent(AppearanceFont font) => -AppearanceFontMetricsData.FamilyExtents[(Family(font) * ExtentsPerFamily) + 1] / UnitsPerEm;

    /// <summary>Gets the WinAnsi code of a character.</summary>
    /// <param name="codePoint">The character.</param>
    /// <param name="code">The code.</param>
    /// <returns><see langword="true"/> when the built in fonts can show the character.</returns>
    public static bool TryEncode(int codePoint, out byte code)
    {
        if (codePoint is (>= FirstCode and < WindowsFirst) or (> WindowsLast and <= byte.MaxValue))
        {
            code = (byte)codePoint;
            return true;
        }

        var index = AppearanceFontMetricsData.WindowsCharacters.IndexOf((ushort)codePoint);
        code = index >= 0 && codePoint > 0 && codePoint <= ushort.MaxValue ? (byte)(WindowsFirst + index) : (byte)0;
        return code != 0;
    }

    /// <summary>Encodes text as WinAnsi codes, leaving out characters the built in fonts cannot show.</summary>
    /// <param name="text">The text.</param>
    /// <param name="destination">The codes; at least as long as the text.</param>
    /// <returns>The number of codes written.</returns>
    public static int Encode(ReadOnlySpan<char> text, Span<byte> destination)
    {
        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (count >= destination.Length || !TryEncode(rune.Value, out var code))
            {
                continue;
            }

            destination[count] = code;
            count++;
        }

        return count;
    }

    /// <summary>Gets the advance of a code.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The WinAnsi code.</param>
    /// <returns>The advance in ems, or zero for a code without a glyph.</returns>
    public static float GetAdvance(AppearanceFont font, byte code)
    {
        var at = Offset(font, code);
        return at < 0 ? 0 : AppearanceFontMetricsData.Data[at] / UnitsPerEm;
    }

    /// <summary>Gets the box around a code's glyph.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The WinAnsi code.</param>
    /// <returns>The box in ems relative to the glyph's origin; empty for a code without a glyph.</returns>
    public static PdfRectangle GetGlyphBox(AppearanceFont font, byte code)
    {
        var at = Offset(font, code);
        if (at < 0)
        {
            return default;
        }

        var data = AppearanceFontMetricsData.Data;
        return new(data[at + LeftOffset] / UnitsPerEm, data[at + BottomOffset] / UnitsPerEm, data[at + RightOffset] / UnitsPerEm, data[at + TopOffset] / UnitsPerEm);
    }

    /// <summary>
    /// Measures the ink of a run of codes written from an origin: the union of every glyph's box, each placed at its
    /// pen position, as PDF readers measure text objects.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <param name="codes">The WinAnsi codes.</param>
    /// <param name="size">The font size in points.</param>
    /// <returns>The box in points relative to the origin, or an empty box for no codes.</returns>
    public static PdfRectangle MeasureInk(AppearanceFont font, ReadOnlySpan<byte> codes, float size)
    {
        if (codes.IsEmpty)
        {
            return default;
        }

        var pen = 0F;
        var left = float.MaxValue;
        var right = float.MinValue;
        var bottom = float.MaxValue;
        var top = float.MinValue;
        foreach (var code in codes)
        {
            var box = GetGlyphBox(font, code);
            left = MathF.Min(left, pen + (box.Left * size));
            right = MathF.Max(right, pen + (box.Right * size));
            bottom = MathF.Min(bottom, box.Bottom * size);
            top = MathF.Max(top, box.Top * size);
            pen += GetAdvance(font, code) * size;
        }

        return new(left, bottom, right, top);
    }

    /// <summary>Measures the advance of a run of codes.</summary>
    /// <param name="font">The font.</param>
    /// <param name="codes">The WinAnsi codes.</param>
    /// <param name="size">The font size in points.</param>
    /// <returns>The width in points.</returns>
    public static float MeasureAdvance(AppearanceFont font, ReadOnlySpan<byte> codes, float size)
    {
        var width = 0F;
        foreach (var code in codes)
        {
            width += GetAdvance(font, code);
        }

        return width * size;
    }

    /// <summary>Creates a font dictionary for a built in font with WinAnsi encoding.</summary>
    /// <param name="owner">The document the dictionary belongs to.</param>
    /// <param name="font">The font.</param>
    /// <returns>The dictionary.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is <see langword="null"/>.</exception>
    public static PdfDictionary CreateFontDictionary(PdfObjectStore owner, AppearanceFont font)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var dictionary = new PdfDictionary(owner, StylesPerFamily);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.Font));
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Type1));
        dictionary.Set(KnownName.BaseFont, PdfValue.FromName(owner.Names.Intern(GetBaseFontName(font))));
        dictionary.Set(KnownName.Encoding, PdfValue.FromName(KnownName.WinAnsiEncoding));
        return dictionary;
    }

    /// <summary>Gets the family of a font: 0 for Helvetica, 1 for Times, 2 for Courier.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The family.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Family(AppearanceFont font) => (uint)font < FontCount ? (int)font / StylesPerFamily : 0;

    /// <summary>Gets where a code's metrics start in <see cref="AppearanceFontMetricsData.Data"/>.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The WinAnsi code.</param>
    /// <returns>The offset, or -1 for a code without metrics.</returns>
    private static int Offset(AppearanceFont font, byte code)
    {
        var index = code - FirstCode;
        if ((uint)index >= CodeCount)
        {
            return -1;
        }

        return ((((uint)font < FontCount ? (int)font : 0) * CodeCount) + index) * ValuesPerCode;
    }
}
