// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <summary>The indirect objects of an FDF file after its catalog (object 1), numbered from 2.</summary>
[DebuggerDisplay("FdfObjects: {Count} objects")]
internal sealed class FdfObjects : IInterchangeObjects
{
    /// <summary>The number of the first object; the catalog is object 1.</summary>
    internal const int FirstNumber = 2;

    /// <summary>The objects in number order.</summary>
    private readonly List<PdfValue> _values = [];

    /// <summary>Gets the number of objects.</summary>
    internal int Count => _values.Count;

    /// <summary>Gets the object at a position.</summary>
    /// <param name="index">The position, from zero.</param>
    /// <returns>The object.</returns>
    internal PdfValue this[int index] => _values[index];

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfValue Add(PdfStream stream) => AddObject(PdfValue.FromStream(stream));

    /// <summary>Gets the id of the object at a position.</summary>
    /// <param name="index">The position, from zero.</param>
    /// <returns>The id.</returns>
    internal static PdfObjectId IdOf(int index) => new(FirstNumber + index, 0);

    /// <summary>Stores an object.</summary>
    /// <param name="value">The object.</param>
    /// <returns>A reference to it.</returns>
    internal PdfValue AddObject(PdfValue value)
    {
        _values.Add(value);
        return PdfValue.FromReference(IdOf(_values.Count - 1));
    }

    /// <summary>Reserves an object number to fill in later.</summary>
    /// <returns>The id.</returns>
    internal PdfObjectId Reserve()
    {
        _values.Add(default);
        return IdOf(_values.Count - 1);
    }

    /// <summary>Fills in a reserved object.</summary>
    /// <param name="id">The reserved id.</param>
    /// <param name="value">The object.</param>
    internal void Set(PdfObjectId id, PdfValue value) => _values[id.Number - FirstNumber] = value;
}
