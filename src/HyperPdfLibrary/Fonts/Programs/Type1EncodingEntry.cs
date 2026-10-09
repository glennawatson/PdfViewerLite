// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>One <c>dup code /name put</c> entry of a Type 1 font's built-in encoding.</summary>
/// <param name="Code">The character code.</param>
/// <param name="Name">The range of the glyph name in the font's name buffer.</param>
[DebuggerDisplay("Code {Code}")]
internal readonly record struct Type1EncodingEntry(int Code, TableRange Name);
