// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <content>Putting regenerated content back into the document.</content>
public sealed partial class PdfPageContent
{
    /// <summary>The entries of a small dictionary.</summary>
    private const int SmallEntries = 4;

    /// <summary>How <see cref="Apply(PdfRegenerateMode)"/> writes the objects that were not changed.</summary>
    private PdfRegenerateMode _applyMode;

    /// <summary>
    /// Writes the regenerated content to the page as one new compressed stream, with any resources the content added. The
    /// edit runs in a transaction, so it can be undone. The content read here is stale afterwards; read the page again.
    /// </summary>
    /// <exception cref="InvalidOperationException">The content belongs to a form, not a page.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Apply() => Apply(PdfRegenerateMode.Preserve);

    /// <summary>Writes the regenerated content to the page, as <see cref="Apply()"/> does.</summary>
    /// <param name="mode">Whether objects nobody changed keep their original bytes or are written from the model.</param>
    /// <exception cref="InvalidOperationException">The content belongs to a form, not a page.</exception>
    public void Apply(PdfRegenerateMode mode)
    {
        if (Page is null)
        {
            throw new InvalidOperationException("Only a page's content can be applied; a form's content is applied through the page that paints it.");
        }

        _applyMode = mode;
        Document.ApplyPageContent(this);
    }

    /// <summary>Writes the regenerated content to the page.</summary>
    /// <param name="cancellationToken">Cancels the work before it starts.</param>
    /// <returns>A task that completes when the page has been changed.</returns>
    /// <exception cref="InvalidOperationException">The content belongs to a form, not a page.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public ValueTask ApplyAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Apply();
        return ValueTask.CompletedTask;
    }

    /// <summary>Stores the regenerated content in the page's object while inside a transaction.</summary>
    internal void CommitToPage()
    {
        var store = Document.Objects;
        var current = PdfPageAnnotations.GetPageDictionary(store, Page!);
        var copy = current.Clone();
        var bytes = Regenerate(_applyMode);
        var contents = store.Add(PdfValue.FromStream(CreateStream(new(store, 1), bytes)));
        copy.Set(KnownName.Contents, PdfValue.FromReference(contents));
        if (BuildResources(store) is { } resources)
        {
            copy.Set(KnownName.Resources, PdfValue.FromDictionary(resources));
        }

        store.Replace(Page!.Id, PdfValue.FromDictionary(copy));
    }

    /// <summary>Stores the regenerated content as a new form XObject.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>A reference to the new form.</returns>
    internal PdfValue CommitAsForm(PdfObjectStore store)
    {
        var bytes = Regenerate();
        var dictionary = FormObject!.Stream.Dictionary.Clone();
        _ = dictionary.Remove(KnownName.Filter);
        _ = dictionary.Remove(KnownName.DecodeParms);
        _ = dictionary.Remove(KnownName.Length);
        if (BuildResources(store) is { } resources)
        {
            dictionary.Set(KnownName.Resources, PdfValue.FromDictionary(resources));
        }

        return PdfValue.FromReference(store.Add(PdfValue.FromStream(CreateStream(dictionary, bytes))));
    }

    /// <summary>Copies the resources with every resource the content added, storing them.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The new resources, or <see langword="null"/> when nothing was added.</returns>
    internal PdfDictionary? BuildResources(PdfObjectStore store)
    {
        if (_pending.Count == 0 && _generated.Count == 0)
        {
            return null;
        }

        var resources = Resources?.Clone() ?? new PdfDictionary(store, SmallEntries);
        var tables = new Dictionary<KnownName, PdfDictionary>();
        AddAll(store, resources, tables, _pending);
        AddAll(store, resources, tables, _generated);
        return resources;
    }

    /// <summary>Makes a stream holding content, Flate-compressed.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="content">The content.</param>
    /// <returns>The stream.</returns>
    private static PdfStream CreateStream(PdfDictionary dictionary, byte[] content)
    {
        var compressed = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(content, ref compressed);
            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            return new(dictionary, compressed.ToArray());
        }
        finally
        {
            compressed.Dispose();
        }
    }

    /// <summary>Stores a list of resources and sets them in the resource tables.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="resources">The resource dictionary being built.</param>
    /// <param name="tables">The copied category tables.</param>
    /// <param name="list">The resources.</param>
    private static void AddAll(PdfObjectStore store, PdfDictionary resources, Dictionary<KnownName, PdfDictionary> tables, List<PendingResource> list)
    {
        foreach (var pending in list)
        {
            if (!tables.TryGetValue(pending.Category, out var table))
            {
                table = resources.GetDictionary(pending.Category)?.Clone() ?? new PdfDictionary(store, SmallEntries);
                tables[pending.Category] = table;
                resources.Set(pending.Category, PdfValue.FromDictionary(table));
            }

            table.Set(pending.Name, Store(store, pending));
        }
    }

    /// <summary>Stores one resource's value.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="pending">The resource.</param>
    /// <returns>The value to set in the table: a reference for streams.</returns>
    private static PdfValue Store(PdfObjectStore store, PendingResource pending)
    {
        if (pending.Form is { } form)
        {
            return form.CommitAsForm(store);
        }

        if (pending.Value.AsStream() is { } stream)
        {
            ImageStreams.Promote(store, stream);
            return PdfValue.FromReference(store.Add(pending.Value));
        }

        return pending.Value;
    }
}
