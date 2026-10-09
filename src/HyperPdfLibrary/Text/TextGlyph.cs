// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>One glyph of a text run, in the run's own space as PDFium's text object items are.</summary>
/// <param name="Code">The character code.</param>
/// <param name="UnicodeStart">The start of the glyph's Unicode text in the device's character buffer.</param>
/// <param name="UnicodeLength">The length of the glyph's Unicode text; 0 when the font gives none.</param>
/// <param name="Along">The glyph's position along the writing direction, in text space units scaled by the font size.</param>
/// <param name="Kerning">The adjustment after the glyph, in thousandths of the font size, as a TJ number gives it.</param>
/// <param name="WidthUnits">The glyph's advance in thousandths of the font size, falling back to its box width.</param>
/// <param name="CharWidth">The glyph's advance scaled by the font size; negative for vertical writing.</param>
/// <param name="Box">The glyph's outline bounds in thousandths of the font size.</param>
/// <param name="VerticalOrigin">The vertical writing origin in thousandths of the font size; zero for horizontal text.</param>
[DebuggerDisplay("TextGlyph: {Code} at {Along}")]
internal readonly record struct TextGlyph(
    int Code,
    int UnicodeStart,
    int UnicodeLength,
    float Along,
    float Kerning,
    int WidthUnits,
    float CharWidth,
    PdfRectangle Box,
    Vector2 VerticalOrigin);
