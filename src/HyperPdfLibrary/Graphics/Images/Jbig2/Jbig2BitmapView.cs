// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// A read-only view of a 1-bit JBIG2 bitmap: rows of <c>(width + 7) / 8</c> bytes, most significant bit first, 1 for
/// black. Pixels outside the bitmap read as 0.
/// </summary>
internal readonly ref struct Jbig2BitmapView
{
    /// <summary>The shift that turns a pixel index into a byte index.</summary>
    private const int ByteShift = 3;

    /// <summary>The mask of a pixel's bit index within its byte.</summary>
    private const int BitMask = 7;

    /// <summary>Initializes a new instance of the <see cref="Jbig2BitmapView"/> struct.</summary>
    /// <param name="data">The rows.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    internal Jbig2BitmapView(ReadOnlySpan<byte> data, int width, int height)
    {
        Data = data;
        Width = width;
        Height = height;
        Stride = (width + BitMask) >> ByteShift;
    }

    /// <summary>Gets the rows.</summary>
    internal ReadOnlySpan<byte> Data { get; }

    /// <summary>Gets the width in pixels.</summary>
    internal int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    internal int Height { get; }

    /// <summary>Gets the bytes in a row.</summary>
    internal int Stride { get; }

    /// <summary>Gets a value indicating whether the bitmap has no pixels.</summary>
    internal bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Gets the bytes of a row.</summary>
    /// <param name="y">The row.</param>
    /// <returns>The row, or an empty span outside the bitmap.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<byte> Row(int y) => (uint)y < (uint)Height ? Data.Slice(y * Stride, Stride) : default;

    /// <summary>Gets a pixel.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The pixel, or 0 outside the bitmap.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetPixel(int x, int y) => (uint)y < (uint)Height ? Jbig2Bits.Get(Data.Slice(y * Stride, Stride), x, Width) : 0;
}
