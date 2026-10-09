// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.Document;

/// <content>Page content objects.</content>
public sealed partial class PdfDocument
{
    /// <summary>The number of page content edits applied.</summary>
    private int _pageContentEdits;

    /// <summary>Reads a page's content into objects that can be inspected, deleted, moved and recoloured.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The content.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfPageContent GetPageContent(int pageIndex) => PdfPageContent.Read(this, pageIndex);

    /// <summary>Drops what the document cached about page content and tells renderers to redraw, after an edit changed a page's content.</summary>
    internal void InvalidatePageContent()
    {
        Volatile.Write(ref _textPages, null);
        OptionalContent.InvalidatePages();
    }

    /// <summary>Writes a page's changed content in a transaction.</summary>
    /// <param name="content">The page content.</param>
    internal void ApplyPageContent(PdfPageContent content)
    {
        RunEdit(
            "Edit page content",
            PdfChangeKinds.Other,
            content,
            static (_, state) => state.CommitToPage());
        _ = Interlocked.Increment(ref _pageContentEdits);
        InvalidatePageContent();
    }

    /// <summary>Runs an edit inside a transaction: the open one, or a new one committed on success and rolled back on failure.</summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="label">The undo label used when no transaction is open.</param>
    /// <param name="kinds">The kinds of change the edit makes.</param>
    /// <param name="state">The arguments the edit reads.</param>
    /// <param name="edit">The edit.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void RunInTransaction<TState>(string label, PdfChangeKinds kinds, TState state, Action<PdfEditTransaction, TState> edit)
        where TState : allows ref struct => RunEdit(label, kinds, state, edit);

    /// <summary>Redraws pages after an undo or redo when page content was ever edited, since the history can bring back old content.</summary>
    /// <param name="changed">Whether the undo or redo changed the document.</param>
    /// <returns>The same value.</returns>
    private bool AfterHistory(bool changed)
    {
        if (changed && Volatile.Read(ref _pageContentEdits) > 0)
        {
            InvalidatePageContent();
        }

        return changed;
    }
}
