// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// The library's font factory: picks the font kind by /Subtype as PDFium's CPDF_Font::Create does. /Type0 is
/// composite, /Type3 draws content streams, /TrueType is TrueType, and every other subtype (/Type1, /MMType1 or a
/// missing one) loads as Type 1.
/// </summary>
public static class PdfFontLoader
{
    /// <summary>Loads a font from its dictionary.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <returns>The font, or <see langword="null"/> when a composite font lacks its descendant or encoding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is <see langword="null"/>.</exception>
    public static PdfFont? Load(PdfDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        var subtype = dictionary.GetName(KnownName.Subtype);
        if (subtype.Is(KnownName.Type0))
        {
            return CompositeFontLoader.Load(dictionary);
        }

        return subtype.Is(KnownName.Type3) ? new Type3Font(dictionary) : SimpleFontLoader.Load(dictionary, subtype.Is(KnownName.TrueType));
    }
}
