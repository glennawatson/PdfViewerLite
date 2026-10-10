// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.Redaction;

/// <summary>Drops the /ToUnicode entries of codes that redaction removed and no text in the document uses any more.</summary>
internal static class ToUnicodePruner
{
    /// <summary>Prunes the maps of every font that lost codes.</summary>
    /// <param name="document">The document, after its pages were redacted.</param>
    /// <param name="tally">The counts and the removed codes.</param>
    /// <param name="cancellationToken">Checked once per page.</param>
    internal static void Run(PdfDocument document, RedactionTally tally, CancellationToken cancellationToken)
    {
        if (tally.RemovedCodes.Count == 0)
        {
            return;
        }

        var stillUsed = new Dictionary<PdfDictionary, HashSet<int>>();
        for (var page = 0; page < document.PageCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Collect(PdfPageContentReader.Read(document, page, cancellationToken), tally.RemovedCodes, stillUsed);
        }

        foreach (var (font, codes) in tally.RemovedCodes)
        {
            var unused = new HashSet<int>(codes);
            if (stillUsed.TryGetValue(font, out var used))
            {
                unused.ExceptWith(used);
            }

            Prune(document.Objects, font, unused, tally);
        }
    }

    /// <summary>Collects the codes that text still shows in fonts of interest, in a content and in the forms it paints.</summary>
    /// <param name="content">The content.</param>
    /// <param name="fonts">The fonts of interest.</param>
    /// <param name="used">Receives the codes still shown.</param>
    private static void Collect(PdfPageContent content, Dictionary<PdfDictionary, HashSet<int>> fonts, Dictionary<PdfDictionary, HashSet<int>> used)
    {
        foreach (var item in content.Objects)
        {
            if (item is PdfTextObject { Font: { } font } text && fonts.ContainsKey(font.Dictionary))
            {
                AddCodes(text, used, font.Dictionary);
            }
            else if (item is PdfFormObject form)
            {
                Collect(form.GetContent(), fonts, used);
            }
        }
    }

    /// <summary>Adds the codes of a text object.</summary>
    /// <param name="text">The text object.</param>
    /// <param name="used">Receives the codes.</param>
    /// <param name="font">The font dictionary.</param>
    private static void AddCodes(PdfTextObject text, Dictionary<PdfDictionary, HashSet<int>> used, PdfDictionary font)
    {
        ref var codes = ref CollectionsMarshal.GetValueRefOrAddDefault(used, font, out _);
        codes ??= [];
        foreach (var glyph in text.Glyphs)
        {
            _ = codes.Add(glyph.Code);
        }
    }

    /// <summary>Rewrites one font's /ToUnicode map.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="font">The font dictionary.</param>
    /// <param name="unused">The codes to drop.</param>
    /// <param name="tally">Receives the count.</param>
    private static void Prune(PdfObjectStore store, PdfDictionary font, HashSet<int> unused, RedactionTally tally)
    {
        var id = font.GetRaw(KnownName.ToUnicode).AsReference();
        if (unused.Count == 0 || !id.IsValid || font.Get(KnownName.ToUnicode).AsStream() is not { } map)
        {
            return;
        }

        var rewritten = ToUnicodeWriter.Remove(map.DecodeToArray(), unused, out var dropped);
        if (rewritten is null)
        {
            return;
        }

        var dictionary = new PdfDictionary(store, 1);
        var compressed = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(rewritten, ref compressed);
            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            StoreEditing.Replace(store, id, PdfValue.FromStream(new(dictionary, compressed.ToArray())));
        }
        finally
        {
            compressed.Dispose();
        }

        tally.ToUnicodeEntriesRemoved += dropped;
    }
}
