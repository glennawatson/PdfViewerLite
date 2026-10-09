// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>A half-open rectangle of sample coordinates: <c>[X0, X1) x [Y0, Y1)</c>.</summary>
/// <param name="X0">The first column.</param>
/// <param name="Y0">The first row.</param>
/// <param name="X1">The column after the last.</param>
/// <param name="Y1">The row after the last.</param>
internal readonly record struct JpxRectangle(int X0, int Y0, int X1, int Y1)
{
    /// <summary>Gets the width, or zero when empty.</summary>
    internal int Width => Math.Max(X1 - X0, 0);

    /// <summary>Gets the height, or zero when empty.</summary>
    internal int Height => Math.Max(Y1 - Y0, 0);

    /// <summary>Gets a value indicating whether the rectangle holds no samples.</summary>
    internal bool IsEmpty => X1 <= X0 || Y1 <= Y0;

    /// <summary>Divides a coordinate by a power of two, rounding up.</summary>
    /// <param name="value">The non-negative coordinate.</param>
    /// <param name="shift">The power of two.</param>
    /// <returns>The rounded quotient.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CeilShift(int value, int shift) => (int)((value + (1L << shift) - 1) >> shift);

    /// <summary>Divides a coordinate by a positive value, rounding up.</summary>
    /// <param name="value">The non-negative coordinate.</param>
    /// <param name="divisor">The positive divisor.</param>
    /// <returns>The rounded quotient.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int CeilDivide(long value, long divisor) => (int)((value + divisor - 1) / divisor);

    /// <summary>Scales the rectangle down by a power of two, rounding each edge up.</summary>
    /// <param name="shift">The power of two.</param>
    /// <returns>The reduced rectangle.</returns>
    internal JpxRectangle Reduce(int shift) => new(CeilShift(X0, shift), CeilShift(Y0, shift), CeilShift(X1, shift), CeilShift(Y1, shift));

    /// <summary>Gets the part of this rectangle inside another.</summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>The intersection, which may be empty.</returns>
    internal JpxRectangle Intersect(in JpxRectangle other) =>
        new(Math.Max(X0, other.X0), Math.Max(Y0, other.Y0), Math.Min(X1, other.X1), Math.Min(Y1, other.Y1));
}
