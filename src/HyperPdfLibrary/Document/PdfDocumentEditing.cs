// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
namespace HyperPdfLibrary.Document;

/// <summary>Manages document edit transactions and cache invalidation.</summary>
public static class PdfDocumentEditing
{
    /// <summary>Gets the undo and redo stacks of committed edits.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The document's edit history.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfEditHistory GetHistory(PdfDocument document) => StoreTransactions.GetHistory(document.Objects);

    /// <summary>
    /// Starts an edit transaction. Page and metadata operations called while it is open join it, so they commit, roll
    /// back and undo together.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="label">A short description of the edit, shown for undo.</param>
    /// <returns>The transaction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A transaction is already open.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfEditTransaction BeginEdit(PdfDocument document, string label) => StoreTransactions.BeginTransaction(document.Objects, label);

    /// <summary>Reverts the most recent committed edit; pages, links, labels and the outline are read again.</summary>
    /// <param name="document">The document.</param>
    /// <returns><see langword="true"/> when an edit was reverted.</returns>
    /// <exception cref="InvalidOperationException">A transaction is open.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Undo(PdfDocument document) => PdfDocumentPageContent.AfterHistory(document, StoreTransactions.GetHistory(document.Objects).Undo());

    /// <summary>Applies the most recently undone edit again.</summary>
    /// <param name="document">The document.</param>
    /// <returns><see langword="true"/> when an edit was applied.</returns>
    /// <exception cref="InvalidOperationException">A transaction is open.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Redo(PdfDocument document) => PdfDocumentPageContent.AfterHistory(document, StoreTransactions.GetHistory(document.Objects).Redo());

    /// <summary>Drops every cache read from the objects, so the next reads see the current edits.</summary>
    /// <param name="document">The document.</param>
    internal static void InvalidateCaches(PdfDocument document)
    {
        Volatile.Write(ref document.State.PageSet, null);
        Volatile.Write(ref document.State.LabelRanges, null);
        Volatile.Write(ref document.State.Outline, null);
        Volatile.Write(ref document.State.Signatures, null);
        Volatile.Write(ref document.State.Attachments, null);
        PdfDocumentLayers.DropOptionalContent(document);
    }

    /// <summary>Runs an edit inside a transaction: the open one, or a new one committed on success and rolled back on failure.</summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="label">The undo label used when no transaction is open.</param>
    /// <param name="kinds">The kinds of change the edit makes.</param>
    /// <param name="state">The arguments the edit reads.</param>
    /// <param name="edit">The edit.</param>
    internal static void RunEdit<TState>(PdfDocument document, string label, PdfChangeKinds kinds, TState state, Action<PdfEditTransaction, TState> edit)
        where TState : allows ref struct
    {
        var transaction = PdfDocumentEditing.JoinOrBegin(document, label, out var owned);
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
            PdfDocumentEditing.InvalidateCaches(document);
        }
    }

    /// <summary>Joins the open transaction, or starts one the caller commits.</summary>
    /// <param name="document">The document.</param>
    /// <param name="label">The description for a new transaction.</param>
    /// <param name="owned">Whether a new transaction was started.</param>
    /// <returns>The transaction.</returns>
    private static PdfEditTransaction JoinOrBegin(PdfDocument document, string label, out bool owned)
    {
        lock (document.Objects.Gate)
        {
            if (StoreTransactions.GetCurrentTransaction(document.Objects) is { } open)
            {
                owned = false;
                return open;
            }

            owned = true;
            return StoreTransactions.BeginTransaction(document.Objects, label);
        }
    }
}
