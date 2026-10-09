// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Fonts.Data;

/// <summary>The built-in 256-code encodings, mapping each code to a glyph name without allocating.</summary>
public static partial class FontEncodings
{
    /// <summary>The number of codes in a simple encoding.</summary>
    internal const int CodeCount = 256;

    /// <summary>Gets the glyph name of a code.</summary>
    /// <param name="encoding">The encoding.</param>
    /// <param name="code">The code.</param>
    /// <returns>The glyph name's ASCII bytes, or empty when the code is unused.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> GetGlyphName(FontEncoding encoding, int code) => GlyphNames.Get(GetNameId(encoding, code));

    /// <summary>Finds the code a glyph name has in an encoding.</summary>
    /// <param name="encoding">The encoding.</param>
    /// <param name="glyphName">The glyph name's bytes.</param>
    /// <returns>The lowest code with that name, or -1.</returns>
    public static int GetCode(FontEncoding encoding, ReadOnlySpan<byte> glyphName)
    {
        var id = GlyphNames.Find(glyphName);
        var table = GetTable(encoding);
        return id == GlyphNames.NoName || table.IsEmpty ? -1 : table.IndexOf((ushort)id);
    }

    /// <summary>Gets the glyph name id of a code.</summary>
    /// <param name="encoding">The encoding.</param>
    /// <param name="code">The code.</param>
    /// <returns>The name id, or <see cref="GlyphNames.NoName"/>.</returns>
    internal static int GetNameId(FontEncoding encoding, int code)
    {
        var table = GetTable(encoding);
        return (uint)code < (uint)table.Length ? table[code] : GlyphNames.NoName;
    }

    /// <summary>Gets the table of an encoding.</summary>
    /// <param name="encoding">The encoding.</param>
    /// <returns>The 256 name ids, or empty for <see cref="FontEncoding.None"/>.</returns>
    private static ReadOnlySpan<ushort> GetTable(FontEncoding encoding) => encoding switch
    {
        FontEncoding.Standard => StandardCodes,
        FontEncoding.WinAnsi => WinAnsiCodes,
        FontEncoding.MacRoman => MacRomanCodes,
        FontEncoding.MacExpert => MacExpertCodes,
        FontEncoding.PdfDoc => PdfDocCodes,
        FontEncoding.Symbol => SymbolCodes,
        FontEncoding.ZapfDingbats => ZapfDingbatsCodes,
        FontEncoding.Expert => ExpertCodes,
        _ => [],
    };
}
