// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Data;

/// <summary>Lookups over the standard strings and the Macintosh glyph order.</summary>
internal static class CffStandardData
{
    /// <summary>The number of CFF standard strings.</summary>
    internal const int StandardStringCount = 391;

    /// <summary>The number of Macintosh standard glyph names.</summary>
    internal const int MacGlyphCount = 258;

    /// <summary>Gets a CFF standard string.</summary>
    /// <param name="sid">The string id, below <see cref="StandardStringCount"/>.</param>
    /// <returns>The string's bytes, or empty when out of range.</returns>
    internal static ReadOnlySpan<byte> GetStandardString(int sid) =>
        (uint)sid < StandardStringCount ? GlyphNames.Get(CffStandardTables.StandardStringNameIds[sid]) : [];

    /// <summary>Gets a Macintosh standard glyph name.</summary>
    /// <param name="index">The index, below <see cref="MacGlyphCount"/>.</param>
    /// <returns>The name's bytes, or empty when out of range.</returns>
    internal static ReadOnlySpan<byte> GetMacGlyphName(int index) =>
        (uint)index < MacGlyphCount ? GlyphNames.Get(CffStandardTables.MacGlyphNameIds[index]) : [];
}
