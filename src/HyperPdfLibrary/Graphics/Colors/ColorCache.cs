// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// A fixed-size, direct-mapped cache from packed 8-bit components (up to four) to opaque BGRA words, for colour spaces
/// whose conversion is expensive. Each slot holds the key and the word in one 64-bit value, so concurrent readers and
/// writers never see a mismatched pair. Lookups and stores do not allocate.
/// </summary>
[DebuggerDisplay("ColorCache: {Slots} slots")]
internal sealed class ColorCache
{
    /// <summary>The most components a key can hold.</summary>
    internal const int MaxComponents = 4;

    /// <summary>The number of slots; a power of two.</summary>
    private const int Slots = 4096;

    /// <summary>The bits the hash is shifted down by to leave a slot index.</summary>
    private const int HashShift = 32 - 12;

    /// <summary>The Knuth multiplicative hash constant.</summary>
    private const uint HashMultiplier = 2_654_435_761;

    /// <summary>The bits of the key within a slot value.</summary>
    private const int KeyShift = 32;

    /// <summary>The slots; zero means empty, because every stored word has an opaque alpha byte.</summary>
    private readonly ulong[] _slots = new ulong[Slots];

    /// <summary>Packs component bytes into a key.</summary>
    /// <param name="components">The sample bytes of one pixel, at most <see cref="MaxComponents"/>.</param>
    /// <returns>The key.</returns>
    internal static uint Pack(ReadOnlySpan<byte> components)
    {
        var key = 0U;
        for (var i = 0; i < components.Length; i++)
        {
            key |= (uint)components[i] << (i * PdfColorSpace.ByteBits);
        }

        return key;
    }

    /// <summary>Looks a key up.</summary>
    /// <param name="key">The packed components.</param>
    /// <param name="word">Receives the BGRA word.</param>
    /// <returns><see langword="true"/> when the key is cached.</returns>
    internal bool TryGet(uint key, out uint word)
    {
        var slot = Volatile.Read(ref _slots[Index(key)]);
        word = (uint)slot;
        return word != 0 && (uint)(slot >> KeyShift) == key;
    }

    /// <summary>Stores a conversion, replacing whatever shared its slot.</summary>
    /// <param name="key">The packed components.</param>
    /// <param name="word">The opaque BGRA word.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Set(uint key, uint word) => Volatile.Write(ref _slots[Index(key)], ((ulong)key << KeyShift) | word);

    /// <summary>Gets the slot of a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The slot index.</returns>
    private static int Index(uint key) => (int)((key * HashMultiplier) >> HashShift);
}
