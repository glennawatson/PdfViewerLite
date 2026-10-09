// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Content;

/// <summary>Where a glyph is drawn and how far the text position moves after it.</summary>
/// <param name="GlyphMatrix">The matrix from glyph space to user space: font matrix, font size, scaling, rise and text matrix.</param>
/// <param name="TextMatrix">The text matrix at the glyph's origin, from text space to user space.</param>
/// <param name="Advance">The distance the text position moved after the glyph, in text space units; x for horizontal fonts, y for vertical ones.</param>
[DebuggerDisplay("GlyphPlacement: advance {Advance}")]
internal readonly record struct GlyphPlacement(Matrix3x2 GlyphMatrix, Matrix3x2 TextMatrix, float Advance);
