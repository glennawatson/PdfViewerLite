// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// A set of changes to indirect dictionaries. Each dictionary is copied once, changed on the copy and written back
/// together, so readers never see a dictionary that is being changed.
/// </summary>
[DebuggerDisplay("FormEdit: {_copies.Count} objects")]
internal sealed class FormEdit
{
    /// <summary>The document's objects.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The copies made so far, by object number.</summary>
    private readonly Dictionary<int, Copy> _copies = [];

    /// <summary>Initializes a new instance of the <see cref="FormEdit"/> class.</summary>
    /// <param name="store">The document's objects.</param>
    internal FormEdit(PdfObjectStore store) => _store = store;

    /// <summary>Gets the document's objects.</summary>
    internal PdfObjectStore Store => _store;

    /// <summary>Gets a value indicating whether anything was copied.</summary>
    internal bool HasChanges => _copies.Count > 0;

    /// <summary>Gets the editable copy of an indirect dictionary.</summary>
    /// <param name="id">The object id.</param>
    /// <returns>The copy; <see langword="null"/> when the id is not valid or the object is not a dictionary.</returns>
    internal PdfDictionary? Edit(PdfObjectId id)
    {
        if (!id.IsValid)
        {
            return null;
        }

        if (_copies.TryGetValue(id.Number, out var existing))
        {
            return existing.Dictionary;
        }

        if (StoreReading.GetDictionary(_store, id) is not { } original)
        {
            return null;
        }

        var copy = original.Clone();
        _copies[id.Number] = new(id, copy);
        return copy;
    }

    /// <summary>Writes every copy back to the document.</summary>
    internal void Commit()
    {
        foreach (var copy in _copies.Values)
        {
            StoreEditing.Replace(_store, copy.Id, PdfValue.FromDictionary(copy.Dictionary));
        }

        _copies.Clear();
    }

    /// <summary>A copied dictionary.</summary>
    /// <param name="Id">The object id.</param>
    /// <param name="Dictionary">The copy.</param>
    [DebuggerDisplay("Copy: {Id}")]
    private sealed record Copy(PdfObjectId Id, PdfDictionary Dictionary);
}
