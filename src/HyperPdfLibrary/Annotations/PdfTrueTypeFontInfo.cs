// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <summary>What embedding a TrueType font as a composite font needs. Lengths in the font's metrics are in 1/1000 em.</summary>
/// <param name="BaseFont">The PostScript name, with a subset tag such as <c>ABCDEF+</c> for a subset.</param>
/// <param name="FontFile">The TrueType font file, whose glyph numbers are the codes written.</param>
/// <param name="Widths">The advance of each glyph, by glyph number.</param>
/// <param name="ToUnicode">The <c>/ToUnicode</c> CMap program mapping glyph numbers to text, or empty for none.</param>
/// <param name="FontBox">The font's bounding box.</param>
[DebuggerDisplay("PdfTrueTypeFontInfo: {BaseFont}")]
public sealed record PdfTrueTypeFontInfo(string BaseFont, byte[] FontFile, float[] Widths, byte[] ToUnicode, PdfRectangle FontBox)
{
    /// <summary>Gets the height above the baseline.</summary>
    public float Ascent { get; init; }

    /// <summary>Gets the depth below the baseline, negative.</summary>
    public float Descent { get; init; }

    /// <summary>Gets the height of capital letters.</summary>
    public float CapHeight { get; init; }

    /// <summary>Gets the slant in degrees anticlockwise from vertical; negative for italic.</summary>
    public float ItalicAngle { get; init; }
}
