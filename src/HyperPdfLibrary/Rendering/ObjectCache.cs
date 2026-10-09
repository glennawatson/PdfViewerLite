// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// A thread-safe cache keyed by object identity or value equality. Values are made outside the lock, so a slow decode
/// does not stall other threads; if two threads race, the first value stored wins and both use it. A null result is
/// cached too, so a damaged object is not decoded again.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
/// <typeparam name="TValue">The value type.</typeparam>
[DebuggerDisplay("ObjectCache: {Count} entries")]
internal sealed class ObjectCache<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    /// <summary>Guards the map.</summary>
    private readonly Lock _gate = new();

    /// <summary>The cached values; null records a failed load.</summary>
    private readonly Dictionary<TKey, TValue?> _map = [];

    /// <summary>Gets the number of cached entries.</summary>
    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _map.Count;
            }
        }
    }

    /// <summary>Gets a value, making it when it is not cached.</summary>
    /// <typeparam name="TState">The type of the state given to the factory.</typeparam>
    /// <param name="key">The key.</param>
    /// <param name="state">The state given to the factory.</param>
    /// <param name="factory">Makes the value; may return null.</param>
    /// <returns>The value, or <see langword="null"/> when it could not be made.</returns>
    internal TValue? GetOrCreate<TState>(TKey key, TState state, Func<TKey, TState, TValue?> factory)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var existing))
            {
                return existing;
            }
        }

        var created = factory(key, state);
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var winner))
            {
                return winner;
            }

            _map[key] = created;
            return created;
        }
    }

    /// <summary>Removes every entry.</summary>
    internal void Clear()
    {
        lock (_gate)
        {
            _map.Clear();
        }
    }
}
