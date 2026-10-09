// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>The parsed pieces of a Type 1 font, with every charstring already decrypted into one buffer.</summary>
/// <param name="CharData">The decrypted charstrings and subroutines, end to end.</param>
/// <param name="Subrs">The range of each subroutine in <paramref name="CharData"/>.</param>
/// <param name="Glyphs">The range of each glyph's charstring in <paramref name="CharData"/>.</param>
/// <param name="Names">The glyph and encoding names, end to end.</param>
/// <param name="GlyphNames">The range of each glyph's name in <paramref name="Names"/>.</param>
/// <param name="Matrix">The font matrix.</param>
/// <param name="BoundingBox">The font bounding box.</param>
/// <param name="UsesStandardEncoding">Whether the built-in encoding is StandardEncoding.</param>
/// <param name="EncodingEntries">The explicit encoding entries: a code and the range of its glyph name in <paramref name="Names"/>.</param>
internal sealed record Type1Parts(
    byte[] CharData,
    TableRange[] Subrs,
    TableRange[] Glyphs,
    byte[] Names,
    TableRange[] GlyphNames,
    FontMatrix Matrix,
    PdfRectangle BoundingBox,
    bool UsesStandardEncoding,
    Type1EncodingEntry[] EncodingEntries);
