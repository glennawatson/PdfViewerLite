// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Fonts.Programs;

/// <content>Direct access to single cmap subtables and 'post' names, as the PDF TrueType glyph rules need.</content>
public sealed partial class TrueTypeProgram
{
    /// <summary>Gets a value indicating whether the font has a Windows Unicode BMP (3,1) or Unicode-platform subtable.</summary>
    internal bool HasUnicodeCmap => _cmaps.UnicodeBmp >= 0 || _cmaps.UnicodeFull >= 0;

    /// <summary>Gets a value indicating whether the font has a Macintosh Roman (1,0) subtable.</summary>
    internal bool HasMacCmap => _cmaps.Mac >= 0;

    /// <summary>Gets a value indicating whether the font has any cmap subtable it can read.</summary>
    internal bool HasAnyCmap => HasUnicodeCmap || HasMacCmap || HasSymbolCmap;

    /// <summary>Gets a value indicating whether the font names its glyphs, through a 'post' table or CFF charset.</summary>
    internal bool HasGlyphNames => !GetGlyphName(1).IsEmpty || !GetGlyphName(0).IsEmpty;

    /// <summary>Looks up a code in the Unicode subtables, without the name fallbacks.</summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns>The glyph id, or zero when unmapped.</returns>
    internal int LookupUnicodeCmap(int codePoint)
    {
        var data = _data.Span;
        var glyph = TrueTypeCmap.Lookup(data, _cmaps.UnicodeBmp, codePoint);
        return glyph != 0 ? glyph : TrueTypeCmap.Lookup(data, _cmaps.UnicodeFull, codePoint);
    }

    /// <summary>Looks up a code in the Macintosh Roman subtable.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The glyph id, or zero when unmapped.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int LookupMacCmap(int code) => TrueTypeCmap.Lookup(_data.Span, _cmaps.Mac, code);

    /// <summary>Looks up a code in the Microsoft symbol subtable, also trying the F000, F100 and F200 pages.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The glyph id, or zero when unmapped.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int LookupSymbolCmap(int code) => LookupSymbol(_data.Span, code);

    /// <summary>Finds a glyph by its 'post' or CFF name only, as FreeType's name index does.</summary>
    /// <param name="name">The glyph name.</param>
    /// <returns>The glyph id, or zero when no glyph has the name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int LookupPostName(ReadOnlySpan<byte> name) => Math.Max(FindGlyphByName(name), 0);
}
