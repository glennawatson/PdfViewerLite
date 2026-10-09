// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Filters;

/// <summary>
/// Controls the memory the large decoder scratch buffers keep between uses. Image decoders rent buffers of many
/// megabytes (JPEG 2000 planes, JBIG2 bitmaps, decoded streams). The shared array pool would keep them until the runtime
/// trims it, so a viewer that has closed a scanned book still holds the book's buffers. These pools keep a bounded
/// amount for reuse and release the rest.
/// </summary>
public static class ScratchPools
{
    /// <summary>The number of bytes the pools may keep until <see cref="Budget"/> is set: 48 MiB, about one decoded 600 dpi page.</summary>
    private const long DefaultBytes = 48L * 1024 * 1024;

    /// <summary>Guards the list of pools.</summary>
    private static readonly Lock Gate = new();

    /// <summary>The trim action of each pool made so far.</summary>
    private static readonly List<Action> Trimmers = [];

    /// <summary>The most bytes the pools may keep.</summary>
    private static long _budget = DefaultBytes;

    /// <summary>The bytes the pools keep now.</summary>
    private static long _retained;

    /// <summary>Gets the number of bytes the pools may keep for reuse until <see cref="Budget"/> is set.</summary>
    public static long DefaultBudget => DefaultBytes;

    /// <summary>Gets the bytes the pools keep for reuse now.</summary>
    public static long RetainedBytes => Volatile.Read(ref _retained);

    /// <summary>Gets or sets the most bytes the pools keep for reuse; lowering it trims the pools at once.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public static long Budget
    {
        get => Volatile.Read(ref _budget);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            Volatile.Write(ref _budget, value);
            if (RetainedBytes > value)
            {
                Trim();
            }
        }
    }

    /// <summary>Releases every buffer the pools keep. Buffers that are in use are not affected.</summary>
    public static void Trim()
    {
        Action[] pools;
        lock (Gate)
        {
            pools = [.. Trimmers];
        }

        foreach (var trim in pools)
        {
            trim();
        }
    }

    /// <summary>Registers a pool so <see cref="Trim"/> reaches it.</summary>
    /// <param name="trim">Releases the pool's buffers.</param>
    internal static void Register(Action trim)
    {
        lock (Gate)
        {
            Trimmers.Add(trim);
        }
    }

    /// <summary>Reserves room for a buffer a pool wants to keep.</summary>
    /// <param name="bytes">The buffer's size.</param>
    /// <returns><see langword="true"/> when the buffer fits in the budget.</returns>
    internal static bool TryReserve(long bytes)
    {
        var limit = Volatile.Read(ref _budget);
        while (true)
        {
            var current = Volatile.Read(ref _retained);
            if (current + bytes > limit)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _retained, current + bytes, current) == current)
            {
                return true;
            }
        }
    }

    /// <summary>Gives back room a pool no longer uses.</summary>
    /// <param name="bytes">The size of the buffer the pool let go.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Release(long bytes) => Interlocked.Add(ref _retained, -bytes);
}
