// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>Reads and writes single pixels in packed 1-bit rows, most significant bit first.</summary>
internal static class Jbig2Bits
{
    /// <summary>The shift that turns a pixel index into a byte index.</summary>
    internal const int ByteShift = 3;

    /// <summary>The mask of a pixel's bit index within its byte.</summary>
    internal const int BitMask = 7;

    /// <summary>The bit of the first pixel in a byte.</summary>
    internal const int FirstPixelBit = 0x80;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Gets the bytes in a row of pixels.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <returns>The row length in bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Stride(int width) => (width + BitMask) >> ByteShift;

    /// <summary>Gets a pixel of a row.</summary>
    /// <param name="row">The row, or an empty span for a row outside the bitmap.</param>
    /// <param name="x">The column.</param>
    /// <param name="width">The width in pixels.</param>
    /// <returns>The pixel, or 0 outside the row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Get(ReadOnlySpan<byte> row, int x, int width) =>
        (uint)x < (uint)width && !row.IsEmpty ? (row[x >> ByteShift] >> (BitMask - (x & BitMask))) & 1 : 0;

    /// <summary>Gets the bits needed to number a count of items: the smallest k with 2^k at least the count.</summary>
    /// <param name="count">The count.</param>
    /// <returns>The bits; 0 for a count of 0 or 1.</returns>
    internal static int CeilLog2(long count) => count <= 1 ? 0 : (sizeof(ulong) * ByteBits) - BitOperations.LeadingZeroCount((ulong)(count - 1));

    /// <summary>Sets a pixel of a row to black.</summary>
    /// <param name="row">The row.</param>
    /// <param name="x">The column, inside the row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void SetBlack(Span<byte> row, int x) => row[x >> ByteShift] |= (byte)(FirstPixelBit >> (x & BitMask));
}
