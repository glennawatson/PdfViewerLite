// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts.Data;

/// <summary>
/// The metrics of one standard 14 font, in thousandths of an em. The values are read from static tables, so copying and
/// querying never allocate.
/// </summary>
/// <param name="Font">The font.</param>
[DebuggerDisplay("StandardFontMetrics: {Font}")]
public readonly record struct StandardFontMetrics(StandardFont Font)
{
    /// <summary>Gets the font's PostScript name.</summary>
    public string Name => Font switch
    {
        StandardFont.Courier => "Courier",
        StandardFont.CourierBold => "Courier-Bold",
        StandardFont.CourierBoldOblique => "Courier-BoldOblique",
        StandardFont.CourierOblique => "Courier-Oblique",
        StandardFont.Helvetica => "Helvetica",
        StandardFont.HelveticaBold => "Helvetica-Bold",
        StandardFont.HelveticaBoldOblique => "Helvetica-BoldOblique",
        StandardFont.HelveticaOblique => "Helvetica-Oblique",
        StandardFont.TimesRoman => "Times-Roman",
        StandardFont.TimesBold => "Times-Bold",
        StandardFont.TimesBoldItalic => "Times-BoldItalic",
        StandardFont.TimesItalic => "Times-Italic",
        StandardFont.Symbol => "Symbol",
        StandardFont.ZapfDingbats => "ZapfDingbats",
        _ => string.Empty,
    };

    /// <summary>Gets the font's bounding box.</summary>
    public PdfRectangle BoundingBox => StandardFonts.GetBoundingBox(Font);

    /// <summary>Gets the ascent; symbol fonts use the top of the bounding box.</summary>
    public float Ascent => StandardFonts.GetAscent(Font);

    /// <summary>Gets the descent, a negative number; symbol fonts use the bottom of the bounding box.</summary>
    public float Descent => StandardFonts.GetDescent(Font);

    /// <summary>Gets the cap height, or zero for symbol fonts.</summary>
    public float CapHeight => StandardFonts.GetCapHeight(Font);

    /// <summary>Gets the x height, or zero for symbol fonts.</summary>
    public float XHeight => StandardFonts.GetXHeight(Font);

    /// <summary>Gets the italic angle in degrees counter-clockwise from vertical.</summary>
    public float ItalicAngle => StandardFonts.GetItalicAngle(Font);

    /// <summary>Gets the dominant vertical stem width.</summary>
    public float StemV => StandardFonts.GetStemV(Font);

    /// <summary>Gets the dominant horizontal stem width.</summary>
    public float StemH => StandardFonts.GetStemH(Font);

    /// <summary>Gets the font descriptor flags.</summary>
    public FontFlags Flags => StandardFonts.GetFlags(Font);

    /// <summary>Gets a value indicating whether the font is bold.</summary>
    public bool IsBold => StandardFonts.IsBold(Font);

    /// <summary>Gets a value indicating whether every glyph has the same width.</summary>
    public bool IsFixedPitch => (Flags & FontFlags.FixedPitch) != 0;

    /// <summary>Gets a value indicating whether the font has serifs.</summary>
    public bool IsSerif => (Flags & FontFlags.Serif) != 0;

    /// <summary>Gets a value indicating whether the font is symbolic.</summary>
    public bool IsSymbolic => (Flags & FontFlags.Symbolic) != 0;

    /// <summary>Gets a value indicating whether the font slants.</summary>
    public bool IsItalic => (Flags & FontFlags.Italic) != 0;

    /// <summary>Gets the font's built-in encoding.</summary>
    public FontEncoding BuiltInEncoding => Font switch
    {
        StandardFont.None => FontEncoding.None,
        StandardFont.Symbol => FontEncoding.Symbol,
        StandardFont.ZapfDingbats => FontEncoding.ZapfDingbats,
        _ => FontEncoding.Standard,
    };

    /// <summary>Finds the width of a glyph.</summary>
    /// <param name="glyphName">The glyph name's bytes.</param>
    /// <param name="width">The width in thousandths of an em.</param>
    /// <returns><see langword="true"/> when the font has the glyph.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetWidth(ReadOnlySpan<byte> glyphName, out float width) => StandardFonts.TryGetWidth(Font, glyphName, out width);

    /// <summary>Finds the width of a code in the font's built-in encoding.</summary>
    /// <param name="code">The code.</param>
    /// <param name="width">The width in thousandths of an em.</param>
    /// <returns><see langword="true"/> when the code names a glyph the font has.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetWidth(int code, out float width) =>
        StandardFonts.TryGetWidth(Font, FontEncodings.GetNameId(BuiltInEncoding, code), out width);
}
