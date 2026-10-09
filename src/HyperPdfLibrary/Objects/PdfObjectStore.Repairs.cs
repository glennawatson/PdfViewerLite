// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Objects;

/// <content>What the store repaired while reading a damaged file.</content>
public sealed partial class PdfObjectStore
{
    /// <summary>Gets a value indicating whether reading has so far repaired damage in the file. Faults inside streams and fonts show only once they are read.</summary>
    public bool HasRepairs => WasRepaired || Context?.HasRepairs == true;

    /// <summary>Gets the distinct repairs and limits reported so far, in the order they were first met.</summary>
    /// <returns>A copy of the reports.</returns>
    public PdfDiagnostic[] GetDiagnostics() => Context?.Snapshot() ?? [];

    /// <summary>Gets the distinct repairs reported so far, without the limit reports.</summary>
    /// <returns>A copy of the repair reports.</returns>
    public PdfDiagnostic[] GetRepairs()
    {
        var all = GetDiagnostics();
        var repairs = new List<PdfDiagnostic>(all.Length);
        foreach (var report in all)
        {
            if (report.Code.IsRepair)
            {
                repairs.Add(report);
            }
        }

        return [.. repairs];
    }

    /// <summary>Gets what the cross-reference table says about an object.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The entry type, free for numbers the table does not hold.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal XrefEntryType GetEntryType(int number) => _xref.GetType(number);

    /// <summary>Gets the file offset of an object, or the number of its object stream.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The location the table gives, or -1 for numbers it does not hold.</returns>
    internal long GetEntryLocation(int number) => (uint)number < (uint)_xref.Size ? _xref.GetLocation(number) : -1;

    /// <summary>Gets the catalog the trailer's /Root names, or finds one when /Root names no dictionary (a stream's dictionary is not a catalog).</summary>
    /// <returns>The catalog, or <see langword="null"/> when the file has none.</returns>
    private PdfDictionary? FindCatalog()
    {
        var root = Trailer.Get(KnownName.Root);
        return root.Kind == PdfKind.Dictionary ? root.AsDictionary() : RebuildCatalog() ?? root.AsDictionary();
    }

    /// <summary>
    /// Finds the catalog when the trailer's /Root does not lead to one: the newest <c>/Type /Catalog</c> object, else a new
    /// catalog over the root of the page tree.
    /// </summary>
    /// <returns>The catalog, or <see langword="null"/> when the file has neither.</returns>
    private PdfDictionary? RebuildCatalog()
    {
        if (Context is { Recovery: false })
        {
            return null;
        }

        PdfObjectId pagesRoot = default;
        for (var number = Size - 1; number > 0; number--)
        {
            PdfOpenContext.ThrowIfCancelled(Context);
            var id = new PdfObjectId(number, 0);
            var value = GetObject(id);
            if (value.Kind != PdfKind.Dictionary || value.AsDictionary() is not { } candidate)
            {
                continue;
            }

            if (candidate.IsName(KnownName.Type, KnownName.Catalog))
            {
                return UseCatalog(id, candidate);
            }

            if (!pagesRoot.IsValid && candidate.IsName(KnownName.Type, KnownName.Pages) && !candidate.ContainsKey(KnownName.Parent))
            {
                pagesRoot = id;
            }
        }

        return pagesRoot.IsValid ? UseCatalog(MakeCatalog(pagesRoot)) : null;
    }

    /// <summary>Makes a catalog over a page tree root.</summary>
    /// <param name="pagesRoot">The page tree root.</param>
    /// <returns>The new catalog's id.</returns>
    private PdfObjectId MakeCatalog(PdfObjectId pagesRoot)
    {
        var catalog = new PdfDictionary(this);
        catalog.Add(KnownName.Type, PdfValue.FromName(KnownName.Catalog));
        catalog.Add(KnownName.Pages, PdfValue.FromReference(pagesRoot));
        return Add(PdfValue.FromDictionary(catalog));
    }

    /// <summary>Points the trailer at a catalog made by <see cref="MakeCatalog"/>.</summary>
    /// <param name="id">The new catalog's id.</param>
    /// <returns>The catalog.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PdfDictionary UseCatalog(PdfObjectId id) => UseCatalog(id, GetObject(id).AsDictionary()!);

    /// <summary>Sets /Root in the trailer to a catalog and reports the repair.</summary>
    /// <param name="id">The id of the catalog the trailer should name.</param>
    /// <param name="catalog">The catalog object itself.</param>
    /// <returns>The same catalog object.</returns>
    private PdfDictionary UseCatalog(PdfObjectId id, PdfDictionary catalog)
    {
        Trailer.Set(KnownName.Root, PdfValue.FromReference(id));
        PdfOpenContext.Report(Context, PdfDiagnosticCode.CatalogRebuilt, "The catalog was missing and was found by scanning.", id.Number, -1);
        return catalog;
    }
}
