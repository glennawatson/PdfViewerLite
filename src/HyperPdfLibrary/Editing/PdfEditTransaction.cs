// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// A group of edits to a <see cref="PdfObjectStore"/> that is committed or rolled back as one. While it is open, the
/// store records each object's edit-layer entry the first time it changes, from any caller, so rollback and undo put
/// back the exact previous objects. Values are recorded by reference: change a copy (for example
/// <see cref="CloneDictionary"/>) and replace the object with it, never a value the store already holds.
/// </summary>
/// <remarks>Thread-safe: every member takes the store's lock. Disposing an open transaction rolls it back.</remarks>
[DebuggerDisplay("PdfEditTransaction: {Label}")]
public sealed class PdfEditTransaction : IDisposable
{
    /// <summary>The state of an open transaction.</summary>
    private const int OpenState = 0;

    /// <summary>The state of a committed transaction.</summary>
    private const int CommittedState = 1;

    /// <summary>The state of a rolled back transaction.</summary>
    private const int RolledBackState = 2;

    /// <summary>The store being edited.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The object numbers already recorded.</summary>
    private readonly HashSet<int> _touched = [];

    /// <summary>The objects as they were before their first change, in order.</summary>
    private readonly List<PdfObjectState> _before = [];

    /// <summary>The trailer keys already recorded, by name id.</summary>
    private readonly HashSet<int> _trailerTouched = [];

    /// <summary>The trailer entries as they were before their first change, in order.</summary>
    private readonly List<PdfTrailerEntry> _trailerBefore = [];

    /// <summary>Open, committed or rolled back.</summary>
    private int _state;

    /// <summary>Initializes a new instance of the <see cref="PdfEditTransaction"/> class.</summary>
    /// <param name="store">The store being edited.</param>
    /// <param name="label">The description.</param>
    internal PdfEditTransaction(PdfObjectStore store, string label)
    {
        _store = store;
        Label = label;
    }

    /// <summary>Gets the description of the edit.</summary>
    public string Label { get; }

    /// <summary>Gets the kinds of change the transaction declared.</summary>
    public PdfChangeKinds Kinds { get; private set; }

    /// <summary>Gets a value indicating whether the transaction is still open.</summary>
    public bool IsOpen => Volatile.Read(ref _state) == OpenState;

    /// <summary>Gets the number of objects changed so far.</summary>
    public int ChangedCount
    {
        get
        {
            lock (_store.Gate)
            {
                return _before.Count;
            }
        }
    }

    /// <summary>Adds a new indirect object.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The new object's id.</returns>
    /// <exception cref="InvalidOperationException">The transaction has ended.</exception>
    public PdfObjectId Add(PdfValue value)
    {
        ThrowIfEnded();
        return _store.Add(value);
    }

    /// <summary>Replaces an indirect object.</summary>
    /// <param name="id">The object id.</param>
    /// <param name="value">The new value.</param>
    /// <exception cref="InvalidOperationException">The transaction has ended.</exception>
    public void Replace(PdfObjectId id, PdfValue value)
    {
        ThrowIfEnded();
        _store.Replace(id, value);
    }

    /// <summary>Deletes an indirect object.</summary>
    /// <param name="id">The object id.</param>
    /// <exception cref="InvalidOperationException">The transaction has ended.</exception>
    public void Delete(PdfObjectId id)
    {
        ThrowIfEnded();
        _store.Delete(id);
    }

    /// <summary>Copies an object's dictionary for editing; nested values are shared, so copy them before changing them too.</summary>
    /// <param name="id">The object id.</param>
    /// <returns>The copy, or <see langword="null"/> when the object is not a dictionary.</returns>
    public PdfDictionary? CloneDictionary(PdfObjectId id) =>
        _store.GetObject(id) is { Kind: PdfKind.Dictionary } value ? value.AsDictionary()!.Clone() : null;

    /// <summary>Sets or removes a trailer entry, such as /Info.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value; null removes the key.</param>
    /// <exception cref="InvalidOperationException">The transaction has ended.</exception>
    public void SetTrailerEntry(PdfName key, PdfValue value)
    {
        lock (_store.Gate)
        {
            ThrowIfEnded();
            if (_trailerTouched.Add(key.Id))
            {
                _trailerBefore.Add(new(key, _store.Trailer.GetRaw(key)));
            }

            _store.SetTrailerLocked(new(key, value));
        }
    }

    /// <summary>Declares kinds of change the transaction makes, for signature reports and undo labels.</summary>
    /// <param name="kinds">The kinds.</param>
    public void AddKinds(PdfChangeKinds kinds)
    {
        lock (_store.Gate)
        {
            Kinds |= kinds;
        }
    }

    /// <summary>Ends the transaction, keeping its changes and pushing it onto the undo stack when it changed anything.</summary>
    /// <exception cref="InvalidOperationException">The transaction has ended.</exception>
    public void Commit()
    {
        lock (_store.Gate)
        {
            ThrowIfEnded();
            var before = _before.ToArray();
            var after = new PdfObjectState[before.Length];
            for (var i = 0; i < before.Length; i++)
            {
                after[i] = _store.CaptureLocked(before[i].Number);
            }

            var trailerBefore = _trailerBefore.ToArray();
            var trailerAfter = new PdfTrailerEntry[trailerBefore.Length];
            for (var i = 0; i < trailerBefore.Length; i++)
            {
                trailerAfter[i] = new(trailerBefore[i].Key, _store.Trailer.GetRaw(trailerBefore[i].Key));
            }

            _state = CommittedState;
            _store.EndTransactionLocked(this);
            _store.RefreshCatalogLocked();
            if (before.Length > 0 || trailerBefore.Length > 0)
            {
                _store.History.PushLocked(new(Label, Kinds, before, after, trailerBefore, trailerAfter));
            }
        }

        _store.NotifyChanged();
    }

    /// <summary>Ends the transaction, putting back every object and trailer entry it changed.</summary>
    /// <exception cref="InvalidOperationException">The transaction has ended.</exception>
    public void Rollback()
    {
        lock (_store.Gate)
        {
            ThrowIfEnded();
            RollbackLocked();
        }

        _store.NotifyChanged();
    }

    /// <summary>Rolls the transaction back when it is still open.</summary>
    public void Dispose()
    {
        lock (_store.Gate)
        {
            if (_state != OpenState)
            {
                return;
            }

            RollbackLocked();
        }

        _store.NotifyChanged();
    }

    /// <summary>Records an object's entry before its first change, while holding the store's lock.</summary>
    /// <param name="state">The entry.</param>
    internal void RecordLocked(in PdfObjectState state)
    {
        if (_state == OpenState && _touched.Add(state.Number))
        {
            _before.Add(state);
        }
    }

    /// <summary>Puts everything back and ends the transaction, while holding the store's lock.</summary>
    private void RollbackLocked()
    {
        _state = RolledBackState;
        _store.RestoreLocked(CollectionsMarshal.AsSpan(_before), CollectionsMarshal.AsSpan(_trailerBefore));
        _store.EndTransactionLocked(this);
    }

    /// <summary>Throws when the transaction has ended.</summary>
    /// <exception cref="InvalidOperationException">The transaction has ended.</exception>
    private void ThrowIfEnded()
    {
        if (Volatile.Read(ref _state) != OpenState)
        {
            throw new InvalidOperationException("The transaction has already been committed or rolled back.");
        }
    }
}
