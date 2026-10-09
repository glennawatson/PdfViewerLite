// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Syntax;

/// <summary>
/// A per-thread stack the parser collects array items and dictionary entries on, so each array or dictionary is
/// allocated once at its final size instead of growing.
/// </summary>
internal sealed class ParseScratch
{
    /// <summary>The initial capacity.</summary>
    private const int InitialCapacity = 256;

    /// <summary>The factor the capacity grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The scratch of the current thread.</summary>
    [ThreadStatic]
    private static ParseScratch? _current;

    /// <summary>The collected values.</summary>
    private PdfValue[] _values = new PdfValue[InitialCapacity];

    /// <summary>The collected keys, parallel to <see cref="_values"/>; unused for arrays.</summary>
    private int[] _keys = new int[InitialCapacity];

    /// <summary>Gets the scratch of the current thread.</summary>
    internal static ParseScratch Current => _current ??= new();

    /// <summary>Gets the number of values on the stack.</summary>
    internal int Count { get; private set; }

    /// <summary>Pushes a value.</summary>
    /// <param name="key">The dictionary key, or zero for an array item.</param>
    /// <param name="value">The value.</param>
    internal void Push(int key, PdfValue value)
    {
        if (Count == _values.Length)
        {
            Array.Resize(ref _values, Count * GrowthFactor);
            Array.Resize(ref _keys, Count * GrowthFactor);
        }

        _keys[Count] = key;
        _values[Count] = value;
        Count++;
    }

    /// <summary>Pops the values pushed since a mark into a new array.</summary>
    /// <param name="mark">The count before the first item was pushed.</param>
    /// <param name="owner">The objects references resolve against.</param>
    /// <returns>The array.</returns>
    internal PdfArray PopArray(int mark, PdfObjectStore? owner)
    {
        var array = new PdfArray(owner, _values.AsSpan(mark, Count - mark));
        Release(mark);
        return array;
    }

    /// <summary>Pops the entries pushed since a mark into a new dictionary.</summary>
    /// <param name="mark">The count before the first entry was pushed.</param>
    /// <param name="owner">The objects references resolve against.</param>
    /// <returns>The dictionary.</returns>
    internal PdfDictionary PopDictionary(int mark, PdfObjectStore? owner)
    {
        var dictionary = new PdfDictionary(owner, Count - mark);
        for (var i = mark; i < Count; i++)
        {
            dictionary.Add(new(_keys[i]), _values[i]);
        }

        Release(mark);
        return dictionary;
    }

    /// <summary>Drops the values above a mark, clearing references so they can be collected.</summary>
    /// <param name="mark">The count to return to.</param>
    internal void Release(int mark)
    {
        _values.AsSpan(mark, Count - mark).Clear();
        Count = mark;
    }
}
