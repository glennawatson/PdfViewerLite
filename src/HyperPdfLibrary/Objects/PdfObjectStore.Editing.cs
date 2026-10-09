// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Objects;

/// <content>Changed, added and deleted objects, kept apart from the parsed ones until the document is saved.</content>
public sealed partial class PdfObjectStore
{
    /// <summary>The highest generation; an object that reaches it is never reused.</summary>
    private const int MaxGeneration = 65_535;

    /// <summary>The objects changed or added since opening, by number.</summary>
    private readonly Dictionary<int, PdfValue> _edits = [];

    /// <summary>The numbers deleted since opening and not reused.</summary>
    private readonly HashSet<int> _deleted = [];

    /// <summary>The generation a freed number takes when it is reused or its free entry is written, by number.</summary>
    private readonly Dictionary<int, int> _freedGenerations = [];

    /// <summary>The next unused object number.</summary>
    private int _nextNumber;

    /// <summary>Gets a value indicating whether any object was changed, added or deleted.</summary>
    public bool HasEdits
    {
        get
        {
            lock (_gate)
            {
                return _edits.Count > 0 || _deleted.Count > 0;
            }
        }
    }

    /// <summary>Adds a new indirect object.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The new object's id.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The document has no object numbers left.</exception>
    public PdfObjectId Add(PdfValue value)
    {
        lock (_gate)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(_nextNumber, PdfLimits.MaxObjectNumber);
            var id = new PdfObjectId(_nextNumber, 0);
            _nextNumber++;
            RecordEdit(id.Number);
            _edits[id.Number] = value;
            if ((uint)id.Number < (uint)_cache.Length)
            {
                Volatile.Write(ref _cache[id.Number], null);
            }

            return id;
        }
    }

    /// <summary>Replaces an indirect object, reusing the number of a deleted one.</summary>
    /// <param name="id">The object id.</param>
    /// <param name="value">The new value.</param>
    /// <exception cref="ArgumentOutOfRangeException">The object number is zero, negative or above the limit.</exception>
    public void Replace(PdfObjectId id, PdfValue value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id.Number);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(id.Number, PdfLimits.MaxObjectNumber);
        lock (_gate)
        {
            RecordEdit(id.Number);
            _edits[id.Number] = value;
            _ = _deleted.Remove(id.Number);
            _nextNumber = Math.Max(_nextNumber, id.Number + 1);

            // A cached copy would be answered before the edit; drop it.
            if ((uint)id.Number < (uint)_cache.Length)
            {
                Volatile.Write(ref _cache[id.Number], null);
            }
        }
    }

    /// <summary>
    /// Deletes an indirect object. Saving incrementally records a free entry whose generation is one higher, so the number
    /// can be reused.
    /// </summary>
    /// <param name="id">The object id; the generation is ignored.</param>
    /// <exception cref="ArgumentOutOfRangeException">The object number is zero, negative or above the limit.</exception>
    public void Delete(PdfObjectId id)
    {
        var number = id.Number;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(number, PdfLimits.MaxObjectNumber);
        lock (_gate)
        {
            var existed = _xref.GetType(number) != XrefEntryType.Free || _freedGenerations.ContainsKey(number);
            var generation = GetGenerationLocked(number);
            RecordEdit(number);
            _ = _edits.Remove(number);
            if ((uint)number < (uint)_cache.Length)
            {
                Volatile.Write(ref _cache[number], null);
            }

            // An object that never reached a file, or is already free, has nothing to record.
            if (existed && !_deleted.Contains(number))
            {
                _freedGenerations[number] = Math.Min(generation + 1, MaxGeneration);
                _ = _deleted.Add(number);
            }
        }
    }

    /// <summary>Gets the numbers of the changed, added and deleted objects, in ascending order.</summary>
    /// <returns>The object numbers.</returns>
    public int[] GetEditedNumbers()
    {
        lock (_gate)
        {
            var numbers = new int[_edits.Count + _deleted.Count];
            _edits.Keys.CopyTo(numbers, 0);
            _deleted.CopyTo(numbers, _edits.Count);
            Array.Sort(numbers);
            return numbers;
        }
    }

    /// <summary>
    /// Takes the changed, added and deleted objects with their generations in one step, so an edit made while a save runs
    /// cannot leave the saved update inconsistent.
    /// </summary>
    /// <param name="size">One more than the highest object number at that moment.</param>
    /// <returns>The objects, in ascending number order.</returns>
    public PdfEditedObject[] GetEditedObjects(out int size)
    {
        lock (_gate)
        {
            var objects = new PdfEditedObject[_edits.Count + _deleted.Count];
            var keys = new int[objects.Length];
            var count = 0;
            foreach (var (number, value) in _edits)
            {
                keys[count] = number;
                objects[count] = new(number, value, false, GetGenerationLocked(number));
                count++;
            }

            foreach (var number in _deleted)
            {
                keys[count] = number;
                objects[count] = new(number, default, true, GetGenerationLocked(number));
                count++;
            }

            Array.Sort(keys, objects);
            size = Size;
            return objects;
        }
    }

    /// <summary>
    /// Gets the generation an object was read with, for writing it back under the same id. A deleted number that is
    /// reused takes the generation of its free entry.
    /// </summary>
    /// <param name="number">The object number.</param>
    /// <returns>The generation, or zero for new objects.</returns>
    public int GetGeneration(int number)
    {
        lock (_gate)
        {
            return GetGenerationLocked(number);
        }
    }

    /// <summary>Forgets every change, after the document has been saved and reopened.</summary>
    internal void ClearEdits()
    {
        lock (_gate)
        {
            _edits.Clear();
            _deleted.Clear();
            _freedGenerations.Clear();
        }
    }

    /// <summary>Gets a changed object, while holding the lock.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="value">The changed value.</param>
    /// <returns><see langword="true"/> when the object was changed or deleted.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetEdited(int number, out PdfValue value)
    {
        if (_edits.TryGetValue(number, out value))
        {
            return true;
        }

        value = default;
        return _deleted.Contains(number);
    }

    /// <summary>Gets an object's generation while holding the lock.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The generation.</returns>
    private int GetGenerationLocked(int number)
    {
        if (_freedGenerations.TryGetValue(number, out var freed))
        {
            return freed;
        }

        return _xref.GetType(number) == XrefEntryType.InFile ? _xref.GetDetail(number) : _xref.GetFreeGeneration(number);
    }
}
