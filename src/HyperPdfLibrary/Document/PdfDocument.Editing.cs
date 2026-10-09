// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;

namespace HyperPdfLibrary.Document;

/// <content>Edit transactions, undo and redo, and the caches they invalidate.</content>
public sealed partial class PdfDocument
{
    /// <summary>Gets the undo and redo stacks of committed edits.</summary>
    public PdfEditHistory History => Objects.History;

    /// <summary>Gets the pages, reading the page tree again when an edit dropped them.</summary>
    private PdfPageSet PageSet
    {
        get
        {
            if (Volatile.Read(ref _pageSet) is { } pages)
            {
                return pages;
            }

            // Two readers may both rebuild; both read the same tree, so either result is right.
            pages = PdfPageSet.Read(Objects);
            _ = Interlocked.CompareExchange(ref _pageSet, pages, null);
            return Volatile.Read(ref _pageSet) ?? pages;
        }
    }

    /// <summary>
    /// Starts an edit transaction. Page and metadata operations called while it is open join it, so they commit, roll
    /// back and undo together.
    /// </summary>
    /// <param name="label">A short description of the edit, shown for undo.</param>
    /// <returns>The transaction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A transaction is already open.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfEditTransaction BeginEdit(string label) => Objects.BeginTransaction(label);

    /// <summary>Reverts the most recent committed edit; pages, links, labels and the outline are read again.</summary>
    /// <returns><see langword="true"/> when an edit was reverted.</returns>
    /// <exception cref="InvalidOperationException">A transaction is open.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Undo() => AfterHistory(Objects.History.Undo());

    /// <summary>Applies the most recently undone edit again.</summary>
    /// <returns><see langword="true"/> when an edit was applied.</returns>
    /// <exception cref="InvalidOperationException">A transaction is open.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Redo() => AfterHistory(Objects.History.Redo());

    /// <summary>Drops every cache read from the objects, so the next reads see the current edits.</summary>
    internal void InvalidateCaches()
    {
        Volatile.Write(ref _pageSet, null);
        Volatile.Write(ref _labelRanges, null);
        Volatile.Write(ref _outline, null);
        Volatile.Write(ref _signatures, null);
        Volatile.Write(ref _attachments, null);
        DropOptionalContent();
    }

    /// <summary>Joins the open transaction, or starts one the caller commits.</summary>
    /// <param name="label">The description for a new transaction.</param>
    /// <param name="owned">Whether a new transaction was started.</param>
    /// <returns>The transaction.</returns>
    private PdfEditTransaction JoinOrBegin(string label, out bool owned)
    {
        lock (Objects.Gate)
        {
            if (Objects.CurrentTransaction is { } open)
            {
                owned = false;
                return open;
            }

            owned = true;
            return Objects.BeginTransaction(label);
        }
    }

    /// <summary>Runs an edit inside a transaction: the open one, or a new one committed on success and rolled back on failure.</summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="label">The undo label used when no transaction is open.</param>
    /// <param name="kinds">The kinds of change the edit makes.</param>
    /// <param name="state">The arguments the edit reads.</param>
    /// <param name="edit">The edit.</param>
    private void RunEdit<TState>(string label, PdfChangeKinds kinds, TState state, Action<PdfEditTransaction, TState> edit)
        where TState : allows ref struct
    {
        var transaction = JoinOrBegin(label, out var owned);
        try
        {
            transaction.AddKinds(kinds);
            edit(transaction, state);
        }
        catch
        {
            if (owned)
            {
                transaction.Rollback();
            }

            throw;
        }

        if (owned)
        {
            transaction.Commit();
        }
        else
        {
            // A joined transaction ends later; the caches must follow this step now.
            InvalidateCaches();
        }
    }
}
