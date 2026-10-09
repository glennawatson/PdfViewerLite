// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Finds a structure tree's elements by dictionary and by element id. Filled while the tree is built, then only read.</summary>
[DebuggerDisplay("StructureIndex: {Count} elements")]
internal sealed class StructureIndex
{
    /// <summary>The elements by dictionary.</summary>
    private readonly Dictionary<PdfDictionary, PdfStructureElement> _byDictionary = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>The elements by <c>/ID</c>; the first element with an id keeps it.</summary>
    private readonly Dictionary<string, PdfStructureElement> _byId = [with(StringComparer.Ordinal)];

    /// <summary>Gets the number of elements.</summary>
    internal int Count => _byDictionary.Count;

    /// <summary>Adds an element.</summary>
    /// <param name="element">The element.</param>
    internal void Add(PdfStructureElement element)
    {
        _ = _byDictionary.TryAdd(element.Dictionary, element);
        if (element.ElementId is { } id)
        {
            _ = _byId.TryAdd(id, element);
        }
    }

    /// <summary>Finds the element built from a dictionary.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>The element, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfStructureElement? Find(PdfDictionary dictionary) => _byDictionary.GetValueOrDefault(dictionary);

    /// <summary>Finds an element by its <c>/ID</c>.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The element, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfStructureElement? FindById(string id) => _byId.GetValueOrDefault(id);
}
