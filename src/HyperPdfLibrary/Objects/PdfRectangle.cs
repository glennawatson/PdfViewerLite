// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Objects;

/// <summary>A rectangle in PDF user space, normalised so <see cref="Left"/> &lt;= <see cref="Right"/> and <see cref="Bottom"/> &lt;= <see cref="Top"/>.</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Bottom">The bottom edge.</param>
/// <param name="Right">The right edge.</param>
/// <param name="Top">The top edge.</param>
[DebuggerDisplay("[{Left} {Bottom} {Right} {Top}]")]
public readonly record struct PdfRectangle(float Left, float Bottom, float Right, float Top)
{
    /// <summary>The number of coordinates in a rectangle array.</summary>
    internal const int Coordinates = 4;

    /// <summary>Gets the width.</summary>
    public float Width => Right - Left;

    /// <summary>Gets the height.</summary>
    public float Height => Top - Bottom;

    /// <summary>Gets a value indicating whether the rectangle has no area.</summary>
    public bool IsEmpty => !(Width > 0 && Height > 0);

    /// <summary>Creates a rectangle from two corners in any order.</summary>
    /// <param name="x1">The first x.</param>
    /// <param name="y1">The first y.</param>
    /// <param name="x2">The second x.</param>
    /// <param name="y2">The second y.</param>
    /// <returns>The normalised rectangle.</returns>
    public static PdfRectangle FromCorners(float x1, float y1, float x2, float y2) =>
        new(MathF.Min(x1, x2), MathF.Min(y1, y2), MathF.Max(x1, x2), MathF.Max(y1, y2));

    /// <summary>Reads a rectangle from an array of four numbers.</summary>
    /// <param name="array">The array.</param>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns><see langword="true"/> when the array holds four numbers.</returns>
    public static bool TryFromArray(PdfArray? array, out PdfRectangle rectangle)
    {
        Span<float> corners = stackalloc float[Coordinates];
        if (array is null || array.ReadNumbers(corners) < Coordinates)
        {
            rectangle = default;
            return false;
        }

        rectangle = FromCorners(corners[0], corners[1], corners[2], corners[3]);
        return true;
    }

    /// <summary>Gets the overlap with another rectangle.</summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>The overlap, empty when they do not overlap.</returns>
    public PdfRectangle Intersect(PdfRectangle other) =>
        new(MathF.Max(Left, other.Left), MathF.Max(Bottom, other.Bottom), MathF.Min(Right, other.Right), MathF.Min(Top, other.Top));

    /// <summary>Gets the smallest rectangle covering this and another.</summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>The union.</returns>
    public PdfRectangle Union(PdfRectangle other) =>
        new(MathF.Min(Left, other.Left), MathF.Min(Bottom, other.Bottom), MathF.Max(Right, other.Right), MathF.Max(Top, other.Top));

    /// <summary>Converts to an array value.</summary>
    /// <param name="owner">The objects references resolve against.</param>
    /// <returns>The array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfArray ToArray(PdfObjectStore? owner) => PdfArray.FromNumbers(owner, [Left, Bottom, Right, Top]);
}
