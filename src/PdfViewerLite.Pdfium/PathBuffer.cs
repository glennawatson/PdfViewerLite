// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// The strokes of an annotation in PDF space, in pooled arrays: every point, stroke after stroke, and each stroke's
/// length. Passed by reference and returned with <see cref="Dispose"/> in a <c>finally</c> block.
/// </summary>
[DebuggerDisplay("PathBuffer: {PointCount} points in {StrokeCount} strokes")]
internal ref struct PathBuffer
{
    /// <summary>The points rented to begin with.</summary>
    private const int InitialPoints = 64;

    /// <summary>The strokes rented to begin with.</summary>
    private const int InitialStrokes = 8;

    /// <summary>How much an array grows when it is full.</summary>
    private const int Growth = 2;

    /// <summary>The points.</summary>
    private FsPointF[] _points;

    /// <summary>The stroke lengths.</summary>
    private int[] _lengths;

    /// <summary>Initializes a new instance of the <see cref="PathBuffer"/> struct.</summary>
    public PathBuffer()
    {
        _points = ArrayPool<FsPointF>.Shared.Rent(InitialPoints);
        _lengths = ArrayPool<int>.Shared.Rent(InitialStrokes);
    }

    /// <summary>Gets the number of points.</summary>
    public int PointCount { get; private set; }

    /// <summary>Gets the number of strokes.</summary>
    public int StrokeCount { get; private set; }

    /// <summary>Gets the points.</summary>
    public readonly Span<FsPointF> Points => _points.AsSpan(0, PointCount);

    /// <summary>Gets the stroke lengths.</summary>
    public readonly ReadOnlySpan<int> Lengths => _lengths.AsSpan(0, StrokeCount);

    /// <summary>Makes room for a stroke and returns where its points go; call <see cref="EndStroke"/> once they are written.</summary>
    /// <param name="length">The stroke's point count.</param>
    /// <returns>The stroke's points.</returns>
    internal Span<FsPointF> BeginStroke(int length)
    {
        Grow(ref _points, PointCount + length);
        Grow(ref _lengths, StrokeCount + 1);
        return _points.AsSpan(PointCount, length);
    }

    /// <summary>Records a stroke whose points were written after <see cref="BeginStroke"/>.</summary>
    /// <param name="length">The points written.</param>
    internal void EndStroke(int length)
    {
        if (length <= 0)
        {
            return;
        }

        _lengths[StrokeCount] = length;
        StrokeCount++;
        PointCount += length;
    }

    /// <summary>Forgets every stroke, keeping the arrays.</summary>
    internal void Clear()
    {
        PointCount = 0;
        StrokeCount = 0;
    }

    /// <summary>Returns the arrays to the pool.</summary>
    internal void Dispose()
    {
        ArrayPool<FsPointF>.Shared.Return(_points);
        ArrayPool<int>.Shared.Return(_lengths);
        _points = [];
        _lengths = [];
        Clear();
    }

    /// <summary>Grows a pooled array, keeping its contents.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array.</param>
    /// <param name="needed">The length needed.</param>
    private static void Grow<T>(ref T[] array, int needed)
    {
        if (needed <= array.Length)
        {
            return;
        }

        var larger = ArrayPool<T>.Shared.Rent(Math.Max(needed, array.Length * Growth));
        array.AsSpan().CopyTo(larger);
        ArrayPool<T>.Shared.Return(array);
        array = larger;
    }
}
