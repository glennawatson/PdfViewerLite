// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>A JP2 palette box (I.5.3.4): entries of one value per column.</summary>
/// <param name="Entries">The values, entry by entry, <see cref="Columns"/> per entry.</param>
/// <param name="Count">The number of entries.</param>
/// <param name="Columns">The number of columns.</param>
/// <param name="Precisions">The bits of each column.</param>
/// <param name="Signed">Whether each column is signed.</param>
[DebuggerDisplay("JpxPalette: {Count} entries x {Columns}")]
internal sealed record JpxPalette(int[] Entries, int Count, int Columns, int[] Precisions, bool[] Signed)
{
    /// <summary>Gets a palette value; indices outside the palette clamp to its ends.</summary>
    /// <param name="index">The palette index.</param>
    /// <param name="column">The column.</param>
    /// <returns>The value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Get(int index, int column) => Entries[(Math.Clamp(index, 0, Count - 1) * Columns) + column];
}
