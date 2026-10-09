// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>A character while a text page is built, the counterpart of PDFium's CharInfo.</summary>
/// <param name="Kind">Where the character came from.</param>
/// <param name="Code">The character code, or -1.</param>
/// <param name="Unicode">The character.</param>
/// <param name="Origin">The origin in user space.</param>
/// <param name="Box">The outline box in user space.</param>
/// <param name="LooseBox">The ascent-to-descent box in user space.</param>
/// <param name="Matrix">The run matrix, or the identity for generated characters.</param>
/// <param name="Run">The run the character belongs to, or -1.</param>
/// <param name="WidthUnits">The glyph's advance in thousandths of the font size; 0 without a glyph.</param>
[DebuggerDisplay("TextBuildChar: {Unicode} {Kind}")]
internal readonly record struct TextBuildChar(
    PdfTextCharKind Kind,
    int Code,
    char Unicode,
    Vector2 Origin,
    PdfRectangle Box,
    PdfRectangle LooseBox,
    Matrix3x2 Matrix,
    int Run,
    int WidthUnits);
