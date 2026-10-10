// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Redaction;

/// <summary>Removes the document information dictionary and the XMP metadata stream.</summary>
internal static class MetadataScrubber
{
    /// <summary>Removes the metadata.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="transaction">The open transaction.</param>
    internal static void Scrub(PdfObjectStore store, PdfEditTransaction transaction)
    {
        if (!store.Trailer.GetRaw(KnownName.Info).IsNull)
        {
            transaction.SetTrailerEntry(KnownName.Info, default);
        }

        var root = store.Trailer.GetRaw(KnownName.Root).AsReference();
        if (!root.IsValid || !store.Catalog.ContainsKey(KnownName.Metadata))
        {
            return;
        }

        var catalog = store.Catalog.Clone();
        _ = catalog.Remove(KnownName.Metadata);
        StoreEditing.Replace(store, root, PdfValue.FromDictionary(catalog));
    }
}
