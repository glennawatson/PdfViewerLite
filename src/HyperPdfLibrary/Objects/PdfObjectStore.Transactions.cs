// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;

namespace HyperPdfLibrary.Objects;

/// <content>Edit transactions, undo and redo, and the original values edits replaced.</content>
public sealed partial class PdfObjectStore
{
    /// <summary>The open transaction, guarded by the lock.</summary>
    private PdfOpenTransaction? _transaction;

    /// <summary>The undo and redo stacks, created on first use.</summary>
    private PdfEditHistory? _history;

    /// <summary>Called after a transaction ends or an edit is undone or redone, so readers drop cached pages.</summary>
    private Action? _changed;

    /// <summary>Gets the undo and redo stacks of committed transactions.</summary>
    public PdfEditHistory History
    {
        get
        {
            if (Volatile.Read(ref _history) is { } history)
            {
                return history;
            }

            _ = Interlocked.CompareExchange(ref _history, new(this), null);
            return Volatile.Read(ref _history)!;
        }
    }

    /// <summary>Gets the open transaction, or <see langword="null"/> when none is open.</summary>
    public PdfEditTransaction? CurrentTransaction
    {
        get
        {
            lock (_gate)
            {
                return _transaction?.Transaction;
            }
        }
    }

    /// <summary>Gets the lock that guards the objects and their edits.</summary>
    internal Lock Gate => _gate;

    /// <summary>
    /// Starts a transaction. Every change to the store until it ends, made through it or not, is recorded so it can be
    /// rolled back, or undone after it is committed.
    /// </summary>
    /// <param name="label">A short description of the edit, shown for undo.</param>
    /// <returns>The transaction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A transaction is already open.</exception>
    public PdfEditTransaction BeginTransaction(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        lock (_gate)
        {
            if (_transaction is not null)
            {
                throw new InvalidOperationException("A transaction is already open on this document.");
            }

            var transaction = new PdfEditTransaction(this, label);
            _transaction = new(transaction);
            return transaction;
        }
    }

    /// <summary>Sets the callback run after a transaction ends or an edit is undone or redone.</summary>
    /// <param name="callback">The callback.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetChangeCallback(Action callback) => Volatile.Write(ref _changed, callback);

    /// <summary>Runs the change callback. Called without holding the lock.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void NotifyChanged() => Volatile.Read(ref _changed)?.Invoke();

    /// <summary>Closes a transaction, while holding the lock.</summary>
    /// <param name="transaction">The transaction.</param>
    internal void EndTransactionLocked(PdfEditTransaction transaction)
    {
        if (ReferenceEquals(_transaction?.Transaction, transaction))
        {
            _transaction = null;
        }
    }

    /// <summary>Gets an object's entry in the edit layer, while holding the lock.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The entry.</returns>
    internal PdfObjectState CaptureLocked(int number)
    {
        var freed = _freedGenerations.TryGetValue(number, out var generation) ? generation : -1;
        if (_edits.TryGetValue(number, out var value))
        {
            return new(number, PdfObjectStateKind.Edited, value, freed);
        }

        return new(number, _deleted.Contains(number) ? PdfObjectStateKind.Deleted : PdfObjectStateKind.Original, default, freed);
    }

    /// <summary>Puts objects and trailer entries back as they were captured, while holding the lock.</summary>
    /// <param name="states">The object entries, applied last to first.</param>
    /// <param name="trailer">The trailer entries, applied last to first.</param>
    internal void RestoreLocked(ReadOnlySpan<PdfObjectState> states, ReadOnlySpan<PdfTrailerEntry> trailer)
    {
        for (var i = states.Length - 1; i >= 0; i--)
        {
            RestoreLocked(states[i]);
        }

        for (var i = trailer.Length - 1; i >= 0; i--)
        {
            SetTrailerLocked(trailer[i]);
        }

        RefreshCatalogLocked();
    }

    /// <summary>Sets or removes a trailer entry, while holding the lock.</summary>
    /// <param name="entry">The key and value; a null value removes the key.</param>
    internal void SetTrailerLocked(in PdfTrailerEntry entry)
    {
        if (entry.Value.IsNull)
        {
            _ = Trailer.Remove(entry.Key);
            return;
        }

        Trailer.Set(entry.Key, entry.Value);
    }

    /// <summary>Reads the catalog again after the object holding it was replaced, while holding the lock.</summary>
    internal void RefreshCatalogLocked()
    {
        if (Trailer.GetDictionary(KnownName.Root) is { } catalog)
        {
            Catalog = catalog;
        }
    }

    /// <summary>Gets an object as the file holds it, ignoring edits.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>A freshly parsed value; null when the file has no such object.</returns>
    internal PdfValue GetOriginal(int number)
    {
        if (number <= 0)
        {
            return default;
        }

        lock (_gate)
        {
            return TryLoad(number, out var value) ? value : default;
        }
    }

    /// <summary>Records an object's entry in the open transaction before it changes, while holding the lock.</summary>
    /// <param name="number">The object number.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordEdit(int number) => _transaction?.Transaction.RecordLocked(CaptureLocked(number));

    /// <summary>Puts one object back as captured, while holding the lock.</summary>
    /// <param name="state">The entry.</param>
    private void RestoreLocked(in PdfObjectState state)
    {
        var number = state.Number;
        RecordEdit(number);
        switch (state.Kind)
        {
            case PdfObjectStateKind.Edited:
            {
                _edits[number] = state.Value;
                _ = _deleted.Remove(number);
                break;
            }

            case PdfObjectStateKind.Deleted:
            {
                _ = _edits.Remove(number);
                _ = _deleted.Add(number);
                break;
            }

            default:
            {
                _ = _edits.Remove(number);
                _ = _deleted.Remove(number);
                break;
            }
        }

        if (state.FreedGeneration >= 0)
        {
            _freedGenerations[number] = state.FreedGeneration;
        }
        else
        {
            _ = _freedGenerations.Remove(number);
        }

        _nextNumber = Math.Max(_nextNumber, number + 1);
        if ((uint)number < (uint)_cache.Length)
        {
            Volatile.Write(ref _cache[number], null);
        }
    }
}
