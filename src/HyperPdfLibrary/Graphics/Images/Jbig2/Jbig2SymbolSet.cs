// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// The symbols a symbol dictionary or text region can use: the stores of the dictionaries it refers to, in order, and
/// optionally the store of symbols being decoded. It does not own the stores.
/// </summary>
[DebuggerDisplay("Jbig2SymbolSet: {Count} symbols")]
internal sealed class Jbig2SymbolSet
{
    /// <summary>The stores, in symbol order.</summary>
    private readonly List<Jbig2SymbolStore> _stores = [];

    /// <summary>Gets the number of symbols across every store.</summary>
    internal int Count
    {
        get
        {
            var count = 0;
            foreach (var store in _stores)
            {
                count += store.Count;
            }

            return count;
        }
    }

    /// <summary>Appends a store's symbols.</summary>
    /// <param name="store">The store.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Add(Jbig2SymbolStore store) => _stores.Add(store);

    /// <summary>Creates a set holding this set's stores followed by another store.</summary>
    /// <param name="store">The store to append.</param>
    /// <returns>The new set.</returns>
    internal Jbig2SymbolSet With(Jbig2SymbolStore store)
    {
        var set = new Jbig2SymbolSet();
        set._stores.AddRange(_stores);
        set._stores.Add(store);
        return set;
    }

    /// <summary>Finds the store and index of a symbol.</summary>
    /// <param name="index">The symbol across every store.</param>
    /// <param name="store">The store holding it.</param>
    /// <param name="local">The symbol's index in that store.</param>
    /// <returns><see langword="false"/> when the index is past the last symbol.</returns>
    internal bool TryFind(long index, [NotNullWhen(true)] out Jbig2SymbolStore? store, out int local)
    {
        store = null;
        local = 0;
        if (index < 0)
        {
            return false;
        }

        foreach (var candidate in _stores)
        {
            if (index < candidate.Count)
            {
                store = candidate;
                local = (int)index;
                return true;
            }

            index -= candidate.Count;
        }

        return false;
    }
}
