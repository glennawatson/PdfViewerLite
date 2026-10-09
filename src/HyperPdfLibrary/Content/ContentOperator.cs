// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Content;

/// <summary>The content stream operators (PDF 32000 Annex A).</summary>
public enum ContentOperator
{
    /// <summary>An operator the library does not know.</summary>
    Unknown = 0,

    /// <summary><c>b</c>: close, fill (non-zero) and stroke.</summary>
    CloseFillStroke = 1,

    /// <summary><c>B</c>: fill (non-zero) and stroke.</summary>
    FillStroke = 2,

    /// <summary><c>b*</c>: close, fill (even-odd) and stroke.</summary>
    CloseFillStrokeEvenOdd = 3,

    /// <summary><c>B*</c>: fill (even-odd) and stroke.</summary>
    FillStrokeEvenOdd = 4,

    /// <summary><c>BDC</c>: begin marked content with properties.</summary>
    BeginMarkedContentProperties = 5,

    /// <summary><c>BI</c>: begin inline image.</summary>
    BeginInlineImage = 6,

    /// <summary><c>BMC</c>: begin marked content.</summary>
    BeginMarkedContent = 7,

    /// <summary><c>BT</c>: begin text object.</summary>
    BeginText = 8,

    /// <summary><c>BX</c>: begin compatibility section.</summary>
    BeginCompatibility = 9,

    /// <summary><c>c</c>: cubic curve.</summary>
    CurveTo = 10,

    /// <summary><c>cm</c>: concatenate matrix.</summary>
    ConcatMatrix = 11,

    /// <summary><c>CS</c>: set stroke colour space.</summary>
    SetStrokeColorSpace = 12,

    /// <summary><c>cs</c>: set fill colour space.</summary>
    SetFillColorSpace = 13,

    /// <summary><c>d</c>: set dash pattern.</summary>
    SetDash = 14,

    /// <summary><c>d0</c>: Type 3 glyph width.</summary>
    SetGlyphWidth = 15,

    /// <summary><c>d1</c>: Type 3 glyph width and bounding box.</summary>
    SetGlyphWidthAndBounds = 16,

    /// <summary><c>Do</c>: paint an XObject.</summary>
    PaintXObject = 17,

    /// <summary><c>DP</c>: marked content point with properties.</summary>
    MarkPointProperties = 18,

    /// <summary><c>EI</c>: end inline image.</summary>
    EndInlineImage = 19,

    /// <summary><c>EMC</c>: end marked content.</summary>
    EndMarkedContent = 20,

    /// <summary><c>ET</c>: end text object.</summary>
    EndText = 21,

    /// <summary><c>EX</c>: end compatibility section.</summary>
    EndCompatibility = 22,

    /// <summary><c>f</c> and <c>F</c>: fill (non-zero).</summary>
    Fill = 23,

    /// <summary><c>f*</c>: fill (even-odd).</summary>
    FillEvenOdd = 24,

    /// <summary><c>G</c>: set stroke grey.</summary>
    SetStrokeGray = 25,

    /// <summary><c>g</c>: set fill grey.</summary>
    SetFillGray = 26,

    /// <summary><c>gs</c>: set graphics state parameters.</summary>
    SetGraphicsState = 27,

    /// <summary><c>h</c>: close subpath.</summary>
    ClosePath = 28,

    /// <summary><c>i</c>: set flatness.</summary>
    SetFlatness = 29,

    /// <summary><c>ID</c>: inline image data.</summary>
    InlineImageData = 30,

    /// <summary><c>j</c>: set line join.</summary>
    SetLineJoin = 31,

    /// <summary><c>J</c>: set line cap.</summary>
    SetLineCap = 32,

    /// <summary><c>K</c>: set stroke CMYK.</summary>
    SetStrokeCmyk = 33,

    /// <summary><c>k</c>: set fill CMYK.</summary>
    SetFillCmyk = 34,

    /// <summary><c>l</c>: line to.</summary>
    LineTo = 35,

    /// <summary><c>m</c>: move to.</summary>
    MoveTo = 36,

    /// <summary><c>M</c>: set miter limit.</summary>
    SetMiterLimit = 37,

    /// <summary><c>MP</c>: marked content point.</summary>
    MarkPoint = 38,

    /// <summary><c>n</c>: end path without painting.</summary>
    EndPath = 39,

    /// <summary><c>q</c>: save graphics state.</summary>
    Save = 40,

    /// <summary><c>Q</c>: restore graphics state.</summary>
    Restore = 41,

    /// <summary><c>re</c>: rectangle.</summary>
    Rectangle = 42,

    /// <summary><c>RG</c>: set stroke RGB.</summary>
    SetStrokeRgb = 43,

    /// <summary><c>rg</c>: set fill RGB.</summary>
    SetFillRgb = 44,

    /// <summary><c>ri</c>: set rendering intent.</summary>
    SetRenderingIntent = 45,

    /// <summary><c>s</c>: close and stroke.</summary>
    CloseStroke = 46,

    /// <summary><c>S</c>: stroke.</summary>
    Stroke = 47,

    /// <summary><c>SC</c>: set stroke colour.</summary>
    SetStrokeColor = 48,

    /// <summary><c>sc</c>: set fill colour.</summary>
    SetFillColor = 49,

    /// <summary><c>SCN</c>: set stroke colour, with patterns.</summary>
    SetStrokeColorN = 50,

    /// <summary><c>scn</c>: set fill colour, with patterns.</summary>
    SetFillColorN = 51,

    /// <summary><c>sh</c>: paint a shading.</summary>
    PaintShading = 52,

    /// <summary><c>T*</c>: next line.</summary>
    NextLine = 53,

    /// <summary><c>Tc</c>: character spacing.</summary>
    SetCharacterSpacing = 54,

    /// <summary><c>Td</c>: move text position.</summary>
    MoveText = 55,

    /// <summary><c>TD</c>: move text position and set leading.</summary>
    MoveTextSetLeading = 56,

    /// <summary><c>Tf</c>: set font and size.</summary>
    SetFont = 57,

    /// <summary><c>Tj</c>: show text.</summary>
    ShowText = 58,

    /// <summary><c>TJ</c>: show text with positioning.</summary>
    ShowTextArray = 59,

    /// <summary><c>TL</c>: set leading.</summary>
    SetLeading = 60,

    /// <summary><c>Tm</c>: set text matrix.</summary>
    SetTextMatrix = 61,

    /// <summary><c>Tr</c>: set text render mode.</summary>
    SetRenderMode = 62,

    /// <summary><c>Ts</c>: set text rise.</summary>
    SetRise = 63,

    /// <summary><c>Tw</c>: word spacing.</summary>
    SetWordSpacing = 64,

    /// <summary><c>Tz</c>: horizontal scaling.</summary>
    SetHorizontalScaling = 65,

    /// <summary><c>v</c>: curve with the first control point at the current point.</summary>
    CurveToV = 66,

    /// <summary><c>w</c>: set line width.</summary>
    SetLineWidth = 67,

    /// <summary><c>W</c>: clip (non-zero).</summary>
    Clip = 68,

    /// <summary><c>W*</c>: clip (even-odd).</summary>
    ClipEvenOdd = 69,

    /// <summary><c>y</c>: curve with the second control point at the end point.</summary>
    CurveToY = 70,

    /// <summary><c>'</c>: next line and show text.</summary>
    NextLineShowText = 71,

    /// <summary><c>"</c>: set spacing, next line and show text.</summary>
    SetSpacingNextLineShowText = 72,
}
