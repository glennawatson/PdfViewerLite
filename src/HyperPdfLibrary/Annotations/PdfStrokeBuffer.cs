// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// Points grouped into strokes, held in pooled arrays so reading and rewriting an annotation's ink list, vertices or
/// leader line allocates nothing per point. Pass it by reference and call <see cref="Dispose"/> in a <c>finally</c>.
/// </summary>
[DebuggerDisplay("PdfStrokeBuffer: {StrokeCount} strokes, {PointCount} points")]
public ref struct PdfStrokeBuffer
{
    /// <summary>The capacity of the first rented arrays.</summary>
    private const int InitialCapacity = 64;

    /// <summary>How much the arrays grow when full.</summary>
    private const int Growth = 2;

    /// <summary>The points, stroke after stroke.</summary>
    private Vector2[]? _points;

    /// <summary>The number of points in each stroke.</summary>
    private int[]? _lengths;

    /// <summary>The first point of the stroke being added.</summary>
    private int _strokeStart;

    /// <summary>Gets the number of points in finished strokes and the stroke being added.</summary>
    public int PointCount { get; private set; }

    /// <summary>Gets the number of finished strokes.</summary>
    public int StrokeCount { get; private set; }

    /// <summary>Gets the points, stroke after stroke.</summary>
    public readonly Span<Vector2> Points => _points is null ? [] : _points.AsSpan(0, PointCount);

    /// <summary>Gets the number of points in each finished stroke.</summary>
    public readonly ReadOnlySpan<int> Lengths => _lengths is null ? [] : _lengths.AsSpan(0, StrokeCount);

    /// <summary>Adds a point to the stroke being built.</summary>
    /// <param name="point">The point.</param>
    public void Add(Vector2 point)
    {
        Ensure(ref _points, PointCount + 1);
        _points![PointCount] = point;
        PointCount++;
    }

    /// <summary>Finishes the stroke being built; a stroke with no points is dropped.</summary>
    public void EndStroke()
    {
        var length = PointCount - _strokeStart;
        if (length <= 0)
        {
            return;
        }

        Ensure(ref _lengths, StrokeCount + 1);
        _lengths![StrokeCount] = length;
        StrokeCount++;
        _strokeStart = PointCount;
    }

    /// <summary>Gets the bounds of every point.</summary>
    /// <returns>The bounds, or an empty rectangle when there are no points.</returns>
    public readonly PdfRectangle GetBounds()
    {
        var points = Points;
        if (points.IsEmpty)
        {
            return default;
        }

        var min = points[0];
        var max = points[0];
        foreach (var point in points)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        return new(min.X, min.Y, max.X, max.Y);
    }

    /// <summary>Forgets every point, keeping the arrays.</summary>
    public void Clear()
    {
        PointCount = 0;
        StrokeCount = 0;
        _strokeStart = 0;
    }

    /// <summary>Returns the arrays to the pool.</summary>
    public void Dispose()
    {
        if (_points is not null)
        {
            ArrayPool<Vector2>.Shared.Return(_points);
            _points = null;
        }

        if (_lengths is not null)
        {
            ArrayPool<int>.Shared.Return(_lengths);
            _lengths = null;
        }

        Clear();
    }

    /// <summary>Makes sure a pooled array holds a number of items, growing it when not.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="array">The array, replaced when it grows.</param>
    /// <param name="needed">The items it must hold.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Ensure<T>(ref T[]? array, int needed)
    {
        if (array is not null && needed <= array.Length)
        {
            return;
        }

        var larger = ArrayPool<T>.Shared.Rent(Math.Max(InitialCapacity, Math.Max(needed, (array?.Length ?? 0) * Growth)));
        if (array is not null)
        {
            array.AsSpan().CopyTo(larger);
            ArrayPool<T>.Shared.Return(array);
        }

        array = larger;
    }
}
