// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// The optional cleanup: page thumbnails, private application data, empty annotation arrays and page resources the
/// content never names. Changed dictionaries are copied and put back; the catalog's /Metadata, /StructTreeRoot,
/// /OutputIntents, /AF, /AcroForm and /OCProperties are never touched. A resource dictionary is cleaned only when every
/// stream that can use it was read: inline images, Type 3 fonts or appearance streams that borrow it keep it whole.
/// </summary>
/// <param name="document">The working copy.</param>
/// <param name="scanner">What the content analysis learned.</param>
/// <param name="items">The steps to run.</param>
/// <param name="names">The optimiser's names.</param>
/// <param name="report">Receives the changes.</param>
[DebuggerDisplay("CleanupPass: {_replaced.Count} resource dictionaries replaced")]
internal sealed class CleanupPass(PdfDocument document, ContentUsageScanner scanner, PdfCleanupItems items, OptimizerNames names, OptimizeReportBuilder report)
{
    /// <summary>The bytes an object's header and trailer take, counted for a dropped object.</summary>
    private const int ObjectOverhead = 20;

    /// <summary>The resource dictionaries already replaced, by object number.</summary>
    private readonly HashSet<int> _replaced = [];

    /// <summary>Gets the resource categories cleaned.</summary>
    private static ReadOnlySpan<int> Categories =>
    [
        (int)KnownName.XObject,
        (int)KnownName.Font,
        (int)KnownName.ExtGState,
        (int)KnownName.ColorSpace,
        (int)KnownName.Pattern,
        (int)KnownName.Shading,
        (int)KnownName.Properties,
    ];

    /// <summary>Runs the cleanup.</summary>
    /// <param name="document">The working copy.</param>
    /// <param name="scanner">What the content analysis learned.</param>
    /// <param name="items">The steps to run.</param>
    /// <param name="names">The optimiser's names.</param>
    /// <param name="report">Receives the changes.</param>
    internal static void Run(PdfDocument document, ContentUsageScanner scanner, PdfCleanupItems items, OptimizerNames names, OptimizeReportBuilder report)
    {
        var pass = new CleanupPass(document, scanner, items, names, report);
        for (var i = 0; i < document.PageCount; i++)
        {
            pass.CleanPage(document.GetPage(i));
        }

        if ((items & PdfCleanupItems.PieceInfo) != 0)
        {
            pass.CleanCatalog();
        }

        document.RefreshAfterOptimizerEdit();
    }

    /// <summary>Gets the stored size of a value's object, for the report.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The raw stream length plus overhead, or the overhead alone.</returns>
    private long SizeOf(PdfValue value) => (document.Objects.Resolve(value).AsStream()?.RawLength ?? 0) + ObjectOverhead;

    /// <summary>Cleans one page.</summary>
    /// <param name="page">The page.</param>
    private void CleanPage(PdfPage page)
    {
        var store = document.Objects;
        if (store.GetObject(page.Id).AsDictionary() is not { } current)
        {
            return;
        }

        var copy = current.Clone();
        var changed = RemoveEntry(copy, names.Thumb, PdfCleanupItems.Thumbnails, page, "Removed the page thumbnail.");
        changed |= RemoveEntry(copy, names.PieceInfo, PdfCleanupItems.PieceInfo, page, "Removed private application data from the page.");
        changed |= RemoveEmptyAnnotations(copy, page);
        changed |= (items & PdfCleanupItems.UnusedResources) != 0 && CleanResources(copy, page);
        if (changed)
        {
            store.Replace(page.Id, PdfValue.FromDictionary(copy));
        }
    }

    /// <summary>Removes one entry when its step is on.</summary>
    /// <param name="page">The page dictionary copy.</param>
    /// <param name="key">The entry.</param>
    /// <param name="step">The step that removes it.</param>
    /// <param name="source">The page.</param>
    /// <param name="description">What was done.</param>
    /// <returns><see langword="true"/> when the entry was removed.</returns>
    private bool RemoveEntry(PdfDictionary page, PdfName key, PdfCleanupItems step, PdfPage source, string description)
    {
        if ((items & step) == 0 || !page.ContainsKey(key))
        {
            return false;
        }

        var size = SizeOf(page.GetRaw(key));
        _ = page.Remove(key);
        report.Changed(PdfOptimizeCategory.Cleanup, source.Id.Number, description, size, 0);
        return true;
    }

    /// <summary>Removes an empty /Annots array.</summary>
    /// <param name="page">The page dictionary copy.</param>
    /// <param name="source">The page.</param>
    /// <returns><see langword="true"/> when it was removed.</returns>
    private bool RemoveEmptyAnnotations(PdfDictionary page, PdfPage source)
    {
        if ((items & PdfCleanupItems.EmptyAnnotations) == 0 || page.GetArray(KnownName.Annots) is not { Count: 0 })
        {
            return false;
        }

        _ = page.Remove(KnownName.Annots);
        report.Changed(PdfOptimizeCategory.Cleanup, source.Id.Number, "Removed an empty annotation array.", ObjectOverhead, 0);
        return true;
    }

    /// <summary>Removes resource entries the page's content never names.</summary>
    /// <param name="page">The page dictionary copy.</param>
    /// <param name="source">The page.</param>
    /// <returns><see langword="true"/> when the page dictionary itself changed.</returns>
    private bool CleanResources(PdfDictionary page, PdfPage source)
    {
        var raw = page.GetRaw(KnownName.Resources);
        if (raw.IsNull || source.Resources is not { } resources)
        {
            // Inherited resources may serve other pages; they stay whole.
            return false;
        }

        if (scanner.UnsafeResources.Contains(resources) || (raw.IsReference && _replaced.Contains(raw.AsReference().Number)))
        {
            return false;
        }

        var used = scanner.UsedNames.GetValueOrDefault(resources);
        if (Clean(resources, used, source.Id.Number) is not { } cleaned)
        {
            return false;
        }

        if (raw.IsReference)
        {
            _ = _replaced.Add(raw.AsReference().Number);
            document.Objects.Replace(raw.AsReference(), PdfValue.FromDictionary(cleaned));
            return false;
        }

        page.Set(KnownName.Resources, PdfValue.FromDictionary(cleaned));
        return true;
    }

    /// <summary>Copies a resource dictionary without the names its content does not use.</summary>
    /// <param name="resources">The resource dictionary.</param>
    /// <param name="used">The names used, or <see langword="null"/> when none are.</param>
    /// <param name="pageNumber">The page's object number, for the report.</param>
    /// <returns>The copy, or <see langword="null"/> when nothing was unused.</returns>
    private PdfDictionary? Clean(PdfDictionary resources, HashSet<ResourceUse>? used, int pageNumber)
    {
        PdfDictionary? copy = null;
        foreach (var category in Categories)
        {
            if (resources.GetDictionary((KnownName)category) is not { } entries)
            {
                continue;
            }

            var kept = new PdfDictionary(document.Objects, entries.Count);
            for (var i = 0; i < entries.Count; i++)
            {
                var name = entries.GetKeyAt(i);
                if (IsKept((KnownName)category, name, used))
                {
                    kept.Set(name, entries.GetValueAt(i));
                }
            }

            if (kept.Count == entries.Count)
            {
                continue;
            }

            copy ??= resources.Clone();
            copy.Set((KnownName)category, PdfValue.FromDictionary(kept));
            var description = string.Create(
                CultureInfo.InvariantCulture,
                $"Removed {entries.Count - kept.Count} unused /{document.Objects.Names.GetString((KnownName)category)} resources.");
            report.Changed(PdfOptimizeCategory.Cleanup, pageNumber, description, 0, 0);
        }

        return copy;
    }

    /// <summary>Determines whether a resource entry stays: it is used, or it is a default colour space used implicitly.</summary>
    /// <param name="category">The category.</param>
    /// <param name="name">The entry's name.</param>
    /// <param name="used">The names used, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it stays.</returns>
    private bool IsKept(KnownName category, PdfName name, HashSet<ResourceUse>? used) =>
        (category == KnownName.ColorSpace && document.Objects.Names.GetSpelling(name).StartsWith("Default"u8))
        || used?.Contains(new(category, name)) == true;

    /// <summary>Removes the catalog's private application data.</summary>
    private void CleanCatalog()
    {
        var store = document.Objects;
        var root = store.Trailer.GetRaw(KnownName.Root);
        if (!root.IsReference || store.Resolve(root).AsDictionary() is not { } catalog || !catalog.ContainsKey(names.PieceInfo))
        {
            return;
        }

        var copy = catalog.Clone();
        var size = SizeOf(copy.GetRaw(names.PieceInfo));
        _ = copy.Remove(names.PieceInfo);
        store.Replace(root.AsReference(), PdfValue.FromDictionary(copy));
        report.Changed(PdfOptimizeCategory.Cleanup, root.AsReference().Number, "Removed private application data from the catalog.", size, 0);
    }
}
