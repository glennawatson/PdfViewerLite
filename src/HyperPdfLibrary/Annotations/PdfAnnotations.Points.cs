// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <content>Point arrays: <c>/Vertices</c>, <c>/CL</c>, <c>/L</c>, <c>/InkList</c> and <c>/QuadPoints</c>.</content>
public static partial class PdfAnnotations
{
    /// <summary>The numbers in a point.</summary>
    private const int PointNumbers = 2;

    /// <summary>The corners of a quadrilateral in <c>/QuadPoints</c>.</summary>
    private const int QuadCorners = 4;

    /// <summary>
    /// Sets a flat array of points, <c>[x1 y1 x2 y2 ...]</c>, such as <c>/Vertices</c> or <c>/CL</c>. The array holds
    /// its numbers inline, so this allocates the array alone.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The key.</param>
    /// <param name="points">The points in user space.</param>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static void SetPoints(PdfDictionary annotation, PdfName key, ReadOnlySpan<Vector2> points)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        annotation.Set(key, PdfValue.FromArray(CreatePointArray(annotation.Owner, points)));
    }

    /// <summary>Reads a flat array of points as one stroke.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The key.</param>
    /// <param name="output">Receives the points as one stroke.</param>
    /// <returns>The number of points read; an odd last number is ignored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static int ReadPoints(PdfDictionary annotation, PdfName key, ref PdfStrokeBuffer output)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var count = AppendPoints(annotation.GetArray(key), ref output);
        output.EndStroke();
        return count;
    }

    /// <summary>Sets <c>/InkList</c>: one flat point array per stroke.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="points">Every point, stroke after stroke, in user space.</param>
    /// <param name="lengths">The number of points in each stroke; strokes that run past the points are dropped.</param>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static void SetInkList(PdfDictionary annotation, ReadOnlySpan<Vector2> points, ReadOnlySpan<int> lengths)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var list = new PdfArray(annotation.Owner, lengths.Length);
        var offset = 0;
        foreach (var length in lengths)
        {
            if (length <= 0 || offset + length > points.Length)
            {
                break;
            }

            list.Add(PdfValue.FromArray(CreatePointArray(annotation.Owner, points.Slice(offset, length))));
            offset += length;
        }

        annotation.Set(KnownName.InkList, PdfValue.FromArray(list));
    }

    /// <summary>Reads <c>/InkList</c>, one stroke per path.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="output">Receives the strokes.</param>
    /// <returns>The number of points read.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static int ReadInkList(PdfDictionary annotation, ref PdfStrokeBuffer output)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (annotation.GetArray(KnownName.InkList) is not { } list)
        {
            return 0;
        }

        var total = 0;
        for (var i = 0; i < list.Count; i++)
        {
            total += AppendPoints(list.GetArray(i), ref output);
            output.EndStroke();
        }

        return total;
    }

    /// <summary>
    /// Sets <c>/QuadPoints</c> from quadrilaterals given as four corners each, in the order readers expect: top left,
    /// top right, bottom left, bottom right.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="corners">The corners in user space, four per quadrilateral.</param>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The corners are not a multiple of four.</exception>
    public static void SetQuadPoints(PdfDictionary annotation, ReadOnlySpan<Vector2> corners)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (corners.Length % QuadCorners != 0)
        {
            throw new ArgumentException("Quadrilaterals have four corners each.", nameof(corners));
        }

        annotation.Set(KnownName.QuadPoints, PdfValue.FromArray(CreatePointArray(annotation.Owner, corners)));
    }

    /// <summary>Creates a flat array of points.</summary>
    /// <param name="owner">The document.</param>
    /// <param name="points">The points.</param>
    /// <returns>The array.</returns>
    private static PdfArray CreatePointArray(PdfObjectStore? owner, ReadOnlySpan<Vector2> points)
    {
        var array = new PdfArray(owner, points.Length * PointNumbers);
        foreach (var point in points)
        {
            array.Add(PdfNumber.ToValue(point.X));
            array.Add(PdfNumber.ToValue(point.Y));
        }

        return array;
    }

    /// <summary>Appends the pairs of numbers of a flat array to the stroke being built.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <param name="output">The buffer.</param>
    /// <returns>The number of points appended.</returns>
    private static int AppendPoints(PdfArray? array, ref PdfStrokeBuffer output)
    {
        if (array is null)
        {
            return 0;
        }

        var count = array.Count / PointNumbers;
        for (var i = 0; i < count; i++)
        {
            output.Add(new(array.GetSingle(i * PointNumbers), array.GetSingle((i * PointNumbers) + 1)));
        }

        return count;
    }
}
