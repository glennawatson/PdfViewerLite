// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Data;

/// <content>Lookups over the standard strings and the Macintosh glyph order.</content>
internal static partial class CffStandardData
{
    /// <summary>Gets a CFF standard string.</summary>
    /// <param name="sid">The string id, below <see cref="StandardStringCount"/>.</param>
    /// <returns>The string's bytes, or empty when out of range.</returns>
    internal static ReadOnlySpan<byte> GetStandardString(int sid) =>
        (uint)sid < StandardStringCount ? GlyphNames.Get(StandardStringNameIds[sid]) : [];

    /// <summary>Gets a Macintosh standard glyph name.</summary>
    /// <param name="index">The index, below <see cref="MacGlyphCount"/>.</param>
    /// <returns>The name's bytes, or empty when out of range.</returns>
    internal static ReadOnlySpan<byte> GetMacGlyphName(int index) =>
        (uint)index < MacGlyphCount ? GlyphNames.Get(MacGlyphNameIds[index]) : [];
}
