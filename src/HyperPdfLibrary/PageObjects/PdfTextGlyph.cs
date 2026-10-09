// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>One glyph a text object shows.</summary>
/// <param name="Code">The character code.</param>
/// <param name="ByteOffset">Where the code's bytes start in the object's string bytes.</param>
/// <param name="ByteLength">The number of bytes the code used.</param>
/// <param name="TextStart">Where the glyph's Unicode text starts in the object's text.</param>
/// <param name="TextLength">The length of the glyph's Unicode text; 0 when the font gives none.</param>
/// <param name="Kerning">The sum of the <c>TJ</c> numbers written just before the glyph, in thousandths of the font size.</param>
/// <param name="Advance">How far the text position moves after the glyph, in text space units, with spacing and scaling applied.</param>
/// <param name="Origin">The glyph origin in user space.</param>
/// <param name="Box">The glyph's box in user space, from its width and the font's ascent and descent.</param>
[DebuggerDisplay("PdfTextGlyph: code {Code} at {Origin}")]
public readonly record struct PdfTextGlyph(
    int Code,
    int ByteOffset,
    int ByteLength,
    int TextStart,
    int TextLength,
    float Kerning,
    float Advance,
    Vector2 Origin,
    PdfRectangle Box);
