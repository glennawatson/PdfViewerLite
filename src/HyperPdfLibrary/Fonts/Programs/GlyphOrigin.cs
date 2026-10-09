// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>The point where a glyph's outline starts, non-zero for the accent of an accented character.</summary>
/// <param name="X">The horizontal offset in font units.</param>
/// <param name="Y">The vertical offset in font units.</param>
[DebuggerDisplay("({X}, {Y})")]
internal readonly record struct GlyphOrigin(float X, float Y);
