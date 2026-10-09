// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>Reads name spellings for the font loaders.</summary>
internal static class PdfNames
{
    /// <summary>Gets the spelling of a name used in a dictionary.</summary>
    /// <param name="owner">The dictionary the name came from, whose document holds the name table.</param>
    /// <param name="name">The name.</param>
    /// <returns>The UTF-8 bytes, or empty when the name cannot be spelled.</returns>
    internal static ReadOnlySpan<byte> Spell(PdfDictionary owner, PdfName name)
    {
        if (name.IsNone)
        {
            return [];
        }

        if (name.IsKnown)
        {
            return PdfNameTable.GetKnownSpelling(name.ToKnownName());
        }

        return owner.Owner is { } store ? store.Names.GetSpelling(name) : [];
    }

    /// <summary>Gets a name entry's spelling.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The UTF-8 bytes, or empty when the entry is not a name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> SpellEntry(PdfDictionary dictionary, PdfName key) => Spell(dictionary, dictionary.GetName(key));

    /// <summary>Gets a font's /BaseFont without its subset tag, as text.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <returns>The name, or empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string BaseFontOf(PdfDictionary dictionary) =>
        Encoding.UTF8.GetString(StandardFonts.StripSubsetTag(SpellEntry(dictionary, KnownName.BaseFont)));
}
