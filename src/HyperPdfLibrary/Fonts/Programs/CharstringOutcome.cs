// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>What a charstring run found besides its outline: the width, and the parts of an accented character.</summary>
/// <param name="Width">The advance width.</param>
/// <param name="HasSeac">Whether the glyph is built from a base and an accent glyph.</param>
/// <param name="AccentX">The accent's x offset.</param>
/// <param name="AccentY">The accent's y offset.</param>
/// <param name="BaseCode">The StandardEncoding code of the base glyph.</param>
/// <param name="AccentCode">The StandardEncoding code of the accent glyph.</param>
[DebuggerDisplay("CharstringOutcome: width {Width}")]
internal readonly record struct CharstringOutcome(float Width, bool HasSeac, float AccentX, float AccentY, int BaseCode, int AccentCode);
