// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// Embeds TrueType fonts as composite fonts (<c>/Type0</c> with a <c>/CIDFontType2</c> descendant, Identity-H
/// encoding and an identity CID to glyph map), so text is written as two-byte glyph numbers.
/// </summary>
public static class PdfEmbeddedFonts
{
    /// <summary>The font descriptor flag for a symbolic font, which every Identity-encoded font is.</summary>
    private const int SymbolicFlag = 4;

    /// <summary>The stem width written when the font does not say; readers use it only for substitution.</summary>
    private const int DefaultStemWidth = 80;

    /// <summary>The entries of a font dictionary.</summary>
    private const int FontEntries = 8;

    /// <summary>The entries of a font descriptor.</summary>
    private const int DescriptorEntries = 11;

    /// <summary>Gets the CID system registry.</summary>
    private static ReadOnlySpan<byte> Registry => "Adobe"u8;

    /// <summary>Gets the CID system ordering.</summary>
    private static ReadOnlySpan<byte> Ordering => "Identity"u8;

    /// <summary>Gets the key of a TrueType font file's uncompressed length.</summary>
    private static ReadOnlySpan<byte> Length1 => "Length1"u8;

    /// <summary>Adds a TrueType font and returns its font dictionary's id, for an appearance's <c>/Font</c> resources.</summary>
    /// <param name="store">The document.</param>
    /// <param name="font">The font.</param>
    /// <returns>The <c>/Type0</c> font's object id.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static PdfObjectId AddTrueTypeFont(PdfObjectStore store, PdfTrueTypeFontInfo font)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(font);
        var name = PdfValue.FromName(store.Names.Intern(font.BaseFont));
        var descendant = new PdfDictionary(store, FontEntries);
        descendant.Set(KnownName.Type, PdfValue.FromName(KnownName.Font));
        descendant.Set(KnownName.Subtype, PdfValue.FromName(KnownName.CIDFontType2));
        descendant.Set(KnownName.BaseFont, name);
        descendant.Set(KnownName.CIDSystemInfo, PdfValue.FromDictionary(CreateSystemInfo(store)));
        descendant.Set(KnownName.FontDescriptor, PdfValue.FromReference(store.Add(PdfValue.FromDictionary(CreateDescriptor(store, font, name)))));
        descendant.Set(KnownName.W, PdfValue.FromArray(CreateWidths(store, font.Widths)));
        descendant.Set(KnownName.CIDToGIDMap, PdfValue.FromName(KnownName.Identity));

        var composite = new PdfDictionary(store, FontEntries);
        composite.Set(KnownName.Type, PdfValue.FromName(KnownName.Font));
        composite.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Type0));
        composite.Set(KnownName.BaseFont, name);
        composite.Set(KnownName.Encoding, PdfValue.FromName(KnownName.IdentityH));
        var descendants = new PdfArray(store, 1);
        descendants.Add(PdfValue.FromReference(store.Add(PdfValue.FromDictionary(descendant))));
        composite.Set(KnownName.DescendantFonts, PdfValue.FromArray(descendants));
        if (font.ToUnicode.Length > 0)
        {
            composite.Set(KnownName.ToUnicode, PdfValue.FromReference(store.Add(PdfValue.FromStream(Compress(store, font.ToUnicode, default)))));
        }

        return store.Add(PdfValue.FromDictionary(composite));
    }

    /// <summary>Creates the Adobe-Identity CID system information.</summary>
    /// <param name="store">The document.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary CreateSystemInfo(PdfObjectStore store)
    {
        var info = new PdfDictionary(store, FontEntries);
        info.Set(KnownName.Registry, PdfValue.FromString(Registry.ToArray()));
        info.Set(KnownName.Ordering, PdfValue.FromString(Ordering.ToArray()));
        info.Set(KnownName.Supplement, PdfValue.FromInteger(0));
        return info;
    }

    /// <summary>Creates the font descriptor and adds the font file.</summary>
    /// <param name="store">The document.</param>
    /// <param name="font">The font.</param>
    /// <param name="name">The font's name.</param>
    /// <returns>The descriptor.</returns>
    private static PdfDictionary CreateDescriptor(PdfObjectStore store, PdfTrueTypeFontInfo font, PdfValue name)
    {
        var descriptor = new PdfDictionary(store, DescriptorEntries);
        descriptor.Set(KnownName.Type, PdfValue.FromName(KnownName.FontDescriptor));
        descriptor.Set(KnownName.FontName, name);
        descriptor.Set(KnownName.Flags, PdfValue.FromInteger(SymbolicFlag));
        descriptor.Set(KnownName.FontBBox, PdfValue.FromArray(font.FontBox.ToArray(store)));
        descriptor.Set(KnownName.ItalicAngle, PdfNumber.ToValue(font.ItalicAngle));
        descriptor.Set(KnownName.Ascent, PdfNumber.ToValue(MathF.Round(font.Ascent)));
        descriptor.Set(KnownName.Descent, PdfNumber.ToValue(MathF.Round(font.Descent)));
        descriptor.Set(KnownName.CapHeight, PdfNumber.ToValue(MathF.Round(font.CapHeight)));
        descriptor.Set(KnownName.StemV, PdfValue.FromInteger(DefaultStemWidth));
        var file = Compress(store, font.FontFile, store.Names.Intern(Length1));
        descriptor.Set(KnownName.FontFile2, PdfValue.FromReference(store.Add(PdfValue.FromStream(file))));
        return descriptor;
    }

    /// <summary>Creates the <c>/W</c> array: one run of widths from glyph 0.</summary>
    /// <param name="store">The document.</param>
    /// <param name="widths">The widths.</param>
    /// <returns>The array.</returns>
    private static PdfArray CreateWidths(PdfObjectStore store, ReadOnlySpan<float> widths)
    {
        var run = new PdfArray(store, widths.Length);
        foreach (var width in widths)
        {
            run.Add(PdfNumber.ToValue(width));
        }

        var array = new PdfArray(store, 1 + 1);
        array.Add(PdfValue.FromInteger(0));
        array.Add(PdfValue.FromArray(run));
        return array;
    }

    /// <summary>Creates a Flate-compressed stream, recording the uncompressed length when a key is given.</summary>
    /// <param name="store">The document.</param>
    /// <param name="data">The data.</param>
    /// <param name="lengthKey">The key of the uncompressed length, or no name.</param>
    /// <returns>The stream.</returns>
    private static PdfStream Compress(PdfObjectStore store, ReadOnlySpan<byte> data, PdfName lengthKey)
    {
        var dictionary = new PdfDictionary(store, FontEntries);
        dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
        if (!lengthKey.IsNone)
        {
            dictionary.Set(lengthKey, PdfValue.FromInteger(data.Length));
        }

        var compressed = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(data, ref compressed);
            return new(dictionary, compressed.ToArray());
        }
        finally
        {
            compressed.Dispose();
        }
    }
}
