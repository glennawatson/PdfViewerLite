// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>The ranges of a Type 1 font's glyph charstrings and glyph names.</summary>
/// <param name="Glyphs">The range of each charstring.</param>
/// <param name="Names">The range of each glyph name.</param>
[DebuggerDisplay("{Glyphs.Length} glyphs")]
internal readonly record struct Type1CharStrings(TableRange[] Glyphs, TableRange[] Names);
