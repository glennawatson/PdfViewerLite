// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Redaction;

/// <summary>Drops the fonts, images, graphics states and property lists a page's content no longer names.</summary>
internal static class ResourcePruner
{
    /// <summary>The categories pruned.</summary>
    private static readonly KnownName[] Categories = [KnownName.Font, KnownName.XObject, KnownName.ExtGState, KnownName.Properties];

    /// <summary>Prunes a page's resources.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="tally">Receives the count.</param>
    internal static void Prune(PdfDocument document, int pageIndex, RedactionTally tally)
    {
        var store = document.Objects;
        var page = PdfDocumentPages.GetPage(document, pageIndex);
        var dictionary = PdfPageAnnotations.GetPageDictionary(store, page);
        if (dictionary.GetDictionary(KnownName.Resources) is not { } resources || !TryScan(dictionary.Get(KnownName.Contents), store.Names, resources, out var used))
        {
            return;
        }

        var copy = resources.Clone();
        var removed = 0;
        for (var i = 0; i < Categories.Length; i++)
        {
            removed += PruneCategory(store, copy, Categories[i], used[i]);
        }

        if (removed == 0)
        {
            return;
        }

        var pageCopy = dictionary.Clone();
        pageCopy.Set(KnownName.Resources, PdfValue.FromDictionary(copy));
        store.Replace(page.Id, PdfValue.FromDictionary(pageCopy));
        tally.ResourcesRemoved += removed;
    }

    /// <summary>Removes the entries of a category that are not used.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="resources">The resource dictionary being edited.</param>
    /// <param name="category">The category.</param>
    /// <param name="used">The names used.</param>
    /// <returns>The number of entries removed.</returns>
    private static int PruneCategory(PdfObjectStore store, PdfDictionary resources, KnownName category, HashSet<PdfName> used)
    {
        if (resources.GetDictionary(category) is not { } table)
        {
            return 0;
        }

        var copy = new PdfDictionary(store, table.Count);
        var removed = 0;
        for (var i = 0; i < table.Count; i++)
        {
            if (used.Contains(table.GetKeyAt(i)))
            {
                copy.Set(table.GetKeyAt(i), table.GetRaw(table.GetKeyAt(i)));
            }
            else
            {
                removed++;
            }
        }

        resources.Set(category, PdfValue.FromDictionary(copy));
        return removed;
    }

    /// <summary>Finds the resource names a page's content uses.</summary>
    /// <param name="contents">The page's /Contents value.</param>
    /// <param name="names">The document's name table.</param>
    /// <param name="resources">The page's resources.</param>
    /// <param name="used">Receives a set of names for each of <see cref="Categories"/>.</param>
    /// <returns><see langword="false"/> when the content cannot be pruned safely: a form without resources of its own would use the page's.</returns>
    private static bool TryScan(PdfValue contents, PdfNameTable names, PdfDictionary resources, out HashSet<PdfName>[] used)
    {
        used = [[], [], [], []];
        var buffer = default(PooledBuffer);
        try
        {
            ContentInterpreter.DecodeContents(contents, ref buffer);
            Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
            var reader = new ContentReader(buffer.WrittenSpan, names, operands);
            while (reader.Next(out var op))
            {
                Note(op, ref reader, used);
            }
        }
        finally
        {
            buffer.Dispose();
        }

        return FormsHaveOwnResources(resources, used[1]);
    }

    /// <summary>Records the resource a name operand refers to.</summary>
    /// <param name="op">The operator.</param>
    /// <param name="reader">The reader holding its operands.</param>
    /// <param name="used">The sets of names.</param>
    private static void Note(ContentOperator op, ref ContentReader reader, HashSet<PdfName>[] used)
    {
        switch (op)
        {
            case ContentOperator.SetFont:
                {
                    _ = used[0].Add(reader.Operand(0).Name);
                    break;
                }

            case ContentOperator.PaintXObject:
                {
                    _ = used[1].Add(reader.Operand(0).Name);
                    break;
                }

            case ContentOperator.SetGraphicsState:
                {
                    _ = used[2].Add(reader.Operand(0).Name);
                    break;
                }

            case ContentOperator.BeginMarkedContentProperties:
                {
                    _ = used[3].Add(reader.Operand(1).Name);
                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Checks that every form the content paints has resources of its own.</summary>
    /// <param name="resources">The page's resources.</param>
    /// <param name="forms">The XObject names used.</param>
    /// <returns><see langword="true"/> when pruning cannot break a form.</returns>
    private static bool FormsHaveOwnResources(PdfDictionary resources, HashSet<PdfName> forms)
    {
        var table = resources.GetDictionary(KnownName.XObject);
        foreach (var name in forms)
        {
            if (table?.Get(name).AsStream() is { } xobject && xobject.Dictionary.IsName(KnownName.Subtype, KnownName.Form) && xobject.Dictionary.GetDictionary(KnownName.Resources) is null)
            {
                return false;
            }
        }

        return true;
    }
}
