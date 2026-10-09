// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>A range of bytes inside font data, already clamped to the data.</summary>
/// <param name="Offset">The first byte.</param>
/// <param name="Length">The number of bytes.</param>
[DebuggerDisplay("TableRange: {Offset} +{Length}")]
internal readonly record struct TableRange(int Offset, int Length)
{
    /// <summary>Gets a value indicating whether the range is empty.</summary>
    internal bool IsEmpty => Length <= 0;

    /// <summary>Creates a range clamped to data of a given length.</summary>
    /// <param name="offset">The first byte.</param>
    /// <param name="length">The number of bytes.</param>
    /// <param name="dataLength">The length of the data.</param>
    /// <returns>The clamped range; empty when it starts outside the data.</returns>
    internal static TableRange Clamp(long offset, long length, int dataLength) =>
        offset < 0 || offset >= dataLength || length <= 0 ? default : new((int)offset, (int)Math.Min(length, dataLength - offset));

    /// <summary>Gets the bytes of the range.</summary>
    /// <param name="data">The font data.</param>
    /// <returns>The bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<byte> Of(ReadOnlySpan<byte> data) => FontBytes.Slice(data, Offset, Length);
}
