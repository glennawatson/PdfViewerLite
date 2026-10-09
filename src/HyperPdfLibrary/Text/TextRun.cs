// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>
/// A run of glyphs shown by one text operator with one state, the counterpart of PDFium's text object. Glyph positions
/// are along the x axis of <see cref="Matrix"/> (the y axis for vertical writing).
/// </summary>
/// <param name="Font">The font.</param>
/// <param name="FontSize">The font size.</param>
/// <param name="RenderMode">The text render mode.</param>
/// <param name="CharSpacing">The character spacing.</param>
/// <param name="Matrix">The matrix from the run's space to user space: horizontal scaling, text matrix and CTM, translated to the first glyph's origin.</param>
/// <param name="Rect">The run's glyph bounds in user space.</param>
/// <param name="FirstGlyph">The index of the run's first glyph in the device's glyph list.</param>
/// <param name="GlyphCount">The number of glyphs.</param>
/// <param name="Marks">The marked content the run is in.</param>
[DebuggerDisplay("TextRun: {GlyphCount} glyphs at {Matrix.Translation}")]
internal readonly record struct TextRun(
    PdfFont Font,
    float FontSize,
    int RenderMode,
    float CharSpacing,
    Matrix3x2 Matrix,
    PdfRectangle Rect,
    int FirstGlyph,
    int GlyphCount,
    TextMarks Marks)
{
    /// <summary>Gets the position of the first glyph's origin in user space.</summary>
    internal Vector2 Position => Matrix.Translation;

    /// <summary>Gets the effective horizontal font size in user space, as PDFium's GetFontSizeH gives it.</summary>
    internal float FontSizeH => MathF.Abs(MathF.Sqrt((Matrix.M11 * Matrix.M11) + (Matrix.M12 * Matrix.M12)) * FontSize);
}
