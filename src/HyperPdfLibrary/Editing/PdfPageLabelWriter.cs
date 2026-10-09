// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Rewrites /PageLabels so every page keeps the label it had before pages moved: a new range starts wherever a page
/// does not continue the previous page's style and number. The tree is written as one /Nums array.
/// </summary>
internal static class PdfPageLabelWriter
{
    /// <summary>Writes the labels.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="labels">The label of each page in its new order.</param>
    internal static void Write(PdfEditTransaction transaction, PdfObjectStore store, ReadOnlySpan<PdfPageLabel> labels)
    {
        var catalog = store.Catalog.Clone();
        catalog.Set(KnownName.PageLabels, PdfValue.FromDictionary(CreateTree(store, labels)));
        PdfPageTreeWriter.ReplaceCatalog(transaction, store, catalog);
    }

    /// <summary>Copies the label styles into the target, so pages that shared a style in the source still share one.</summary>
    /// <param name="labels">The labels, with styles from the source.</param>
    /// <param name="sink">The page copier.</param>
    /// <returns>The labels with styles in the target.</returns>
    internal static PdfPageLabel[] Retarget(ReadOnlySpan<PdfPageLabel> labels, IPdfCarrySink sink)
    {
        var styles = new Dictionary<PdfDictionary, PdfDictionary?>(ReferenceEqualityComparer.Instance);
        var result = new PdfPageLabel[labels.Length];
        for (var i = 0; i < result.Length; i++)
        {
            if (labels[i].Style is not { } style)
            {
                result[i] = labels[i];
                continue;
            }

            ref var copy = ref CollectionsMarshal.GetValueRefOrAddDefault(styles, style, out var exists);
            if (!exists)
            {
                copy = sink.Import(PdfValue.FromDictionary(style)).AsDictionary();
            }

            result[i] = labels[i] with { Style = copy };
        }

        return result;
    }

    /// <summary>Builds the <c>/PageLabels</c> number tree.</summary>
    /// <param name="store">The document's objects, or <see langword="null"/> for a new document.</param>
    /// <param name="labels">The label of each page in its order.</param>
    /// <returns>The tree: one /Nums array.</returns>
    internal static PdfDictionary CreateTree(PdfObjectStore? store, ReadOnlySpan<PdfPageLabel> labels)
    {
        var numbers = new PdfArray(store);
        for (var i = 0; i < labels.Length; i++)
        {
            var label = labels[i];
            if (i > 0 && ReferenceEquals(label.Style, labels[i - 1].Style) && label.Number == labels[i - 1].Number + 1)
            {
                continue;
            }

            numbers.Add(PdfValue.FromInteger(i));
            numbers.Add(PdfValue.FromDictionary(CreateRange(store, label)));
        }

        var tree = new PdfDictionary(store);
        tree.Set(KnownName.Nums, PdfValue.FromArray(numbers));
        return tree;
    }

    /// <summary>Creates a label range dictionary that starts at a label.</summary>
    /// <param name="store">The document's objects, or <see langword="null"/> for a new document.</param>
    /// <param name="label">The first page's label.</param>
    /// <returns>The range dictionary.</returns>
    private static PdfDictionary CreateRange(PdfObjectStore? store, PdfPageLabel label)
    {
        var range = new PdfDictionary(store);
        var style = label.Style;
        for (var i = 0; style is not null && i < style.Count; i++)
        {
            var key = style.GetKeyAt(i);
            if (!key.Is(KnownName.St))
            {
                range.Add(key, style.GetValueAt(i));
            }
        }

        if (label.Number != 1)
        {
            range.Set(KnownName.St, PdfValue.FromInteger(label.Number));
        }

        return range;
    }
}
