// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// What the carriers of one page copy share: the copier, the source pages being copied and a working copy of the target's
/// catalog that they edit and <see cref="Flush"/> writes once.
/// </summary>
[DebuggerDisplay("PdfCarryContext: {_pages.Count} pages")]
internal sealed class PdfCarryContext
{
    /// <summary>The source object numbers of the pages being copied.</summary>
    private readonly HashSet<int> _pages = [];

    /// <summary>The working copy of the target's catalog, made on first use.</summary>
    private PdfDictionary? _catalog;

    /// <summary>The working copy of the target's /Names dictionary, made on first use.</summary>
    private PdfDictionary? _names;

    /// <summary>Initializes a new instance of the <see cref="PdfCarryContext"/> class.</summary>
    /// <param name="sink">The page copier.</param>
    internal PdfCarryContext(IPdfCarrySink sink) => Sink = sink;

    /// <summary>Gets the page copier.</summary>
    internal IPdfCarrySink Sink { get; }

    /// <summary>Gets the objects copied from.</summary>
    internal PdfObjectStore Source => Sink.Source;

    /// <summary>Gets the objects copied to, or <see langword="null"/> for a new document.</summary>
    internal PdfObjectStore? TargetStore => Sink.TargetStore;

    /// <summary>Gets or sets a value indicating whether a carrier changed the working catalog, so <see cref="Flush"/> must write it.</summary>
    internal bool CatalogChanged { get; set; }

    /// <summary>Gets the working copy of the target's catalog.</summary>
    internal PdfDictionary Catalog => _catalog ??= Sink.TargetCatalog.Clone();

    /// <summary>Gets the working copy of the target's /Names dictionary, which <see cref="Flush"/> writes back.</summary>
    internal PdfDictionary Names => _names ??= (Catalog.GetDictionary(KnownName.Names)?.Clone() ?? NewDictionary());

    /// <summary>Notes that a source page is being copied, so links to it can be kept.</summary>
    /// <param name="source">The source page's object.</param>
    /// <param name="target">The copy's object.</param>
    internal void AddPage(PdfObjectId source, PdfObjectId target)
    {
        if (!source.IsValid)
        {
            return;
        }

        Sink.MapObject(source, target);
        _ = _pages.Add(source.Number);
    }

    /// <summary>Determines whether a source object is a page that is being copied.</summary>
    /// <param name="number">The source object number.</param>
    /// <returns><see langword="true"/> when links to it can be kept.</returns>
    internal bool IsCopiedPage(int number) => _pages.Contains(number) && Sink.GetMapped(number) > 0;

    /// <summary>Creates an empty dictionary for the target.</summary>
    /// <returns>The dictionary.</returns>
    internal PdfDictionary NewDictionary() => new(TargetStore);

    /// <summary>Sets a dictionary entry, writing the object it already names when it is indirect.</summary>
    /// <param name="holder">The dictionary holding the entry.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The new dictionary.</param>
    internal void SetEntry(PdfDictionary holder, PdfName key, PdfDictionary value)
    {
        var existing = holder.GetRaw(key).AsReference();
        if (existing.IsValid)
        {
            Sink.Set(existing, PdfValue.FromDictionary(value));
            return;
        }

        holder.Set(key, PdfValue.FromDictionary(value));
    }

    /// <summary>Adds a new indirect object.</summary>
    /// <param name="value">The value.</param>
    /// <returns>A reference to it.</returns>
    internal PdfValue AddObject(PdfValue value)
    {
        var id = Sink.Reserve();
        Sink.Set(id, value);
        return PdfValue.FromReference(id);
    }

    /// <summary>Writes the working copies back to the target.</summary>
    internal void Flush()
    {
        if (_names is not null)
        {
            SetEntry(Catalog, KnownName.Names, _names);
            CatalogChanged = true;
        }

        if (CatalogChanged)
        {
            Sink.SetCatalog(Catalog);
        }
    }
}
