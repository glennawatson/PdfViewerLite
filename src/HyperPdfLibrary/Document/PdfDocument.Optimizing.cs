// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Document;

/// <content>A private working copy for the optimiser.</content>
public sealed partial class PdfDocument
{
    /// <summary>
    /// Opens a private copy of the document over the same file, with the unsaved edits copied in, so the optimiser can
    /// change pages without touching the document the caller shows. The copy shares this document's security handler and
    /// file, so it must not be disposed; it is collected when no longer used.
    /// </summary>
    /// <returns>The copy.</returns>
    /// <exception cref="PdfException">The file cannot be read again.</exception>
    internal PdfDocument OpenWorkingCopy()
    {
        var store = Objects.OpenRevision(Objects.Source.Length);
        new StoreValueTranslator(Objects, store).ReplayEdits();
        lock (store.Gate)
        {
            store.RefreshCatalogLocked();
        }

        return new(store);
    }

    /// <summary>Drops every cache read from the objects, including marked content and the structure tree, after the optimiser changed pages.</summary>
    internal void RefreshAfterOptimizerEdit()
    {
        InvalidateCaches();
        Volatile.Write(ref _markedContent, null);
        Volatile.Write(ref _structureTree, null);
        lock (Objects.Gate)
        {
            Objects.RefreshCatalogLocked();
        }
    }
}
