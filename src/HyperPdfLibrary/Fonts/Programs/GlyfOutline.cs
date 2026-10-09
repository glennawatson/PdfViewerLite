// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>
/// Decodes simple TrueType 'glyf' records into quadratic outlines. Points of small glyphs live on the stack; large
/// glyphs rent pooled arrays, so decoding does not allocate.
/// </summary>
internal static class GlyfOutline
{
    /// <summary>The size of the glyph header: contour count and bounding box.</summary>
    internal const int HeaderSize = 10;

    /// <summary>The most points decoded on the stack.</summary>
    private const int StackPoints = 256;

    /// <summary>The number of coordinates per point.</summary>
    private const int Axes = 2;

    /// <summary>The flag of a one-byte x delta.</summary>
    private const int XShort = 0x02;

    /// <summary>The flag of a one-byte y delta.</summary>
    private const int YShort = 0x04;

    /// <summary>The flag that repeats the previous flag.</summary>
    private const int Repeat = 0x08;

    /// <summary>The flag of a positive short x delta, or of an unchanged x.</summary>
    private const int XSameOrPositive = 0x10;

    /// <summary>The flag of a positive short y delta, or of an unchanged y.</summary>
    private const int YSameOrPositive = 0x20;

    /// <summary>The divisor for the midpoint between two off-curve points.</summary>
    private const float Half = 0.5F;

    /// <summary>Decodes a simple glyph.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="record">The glyph record.</param>
    /// <param name="contourCount">The number of contours.</param>
    /// <param name="transform">The transform to apply to every point.</param>
    /// <param name="sink">The sink.</param>
    internal static void DecodeSimple<TSink>(ReadOnlySpan<byte> record, int contourCount, in FontMatrix transform, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var endPointsEnd = HeaderSize + (contourCount * FontBytes.U16Size);
        if (contourCount <= 0 || endPointsEnd + FontBytes.U16Size > record.Length)
        {
            return;
        }

        var pointCount = FontBytes.U16(record, endPointsEnd - FontBytes.U16Size) + 1;
        var flagsOffset = endPointsEnd + FontBytes.U16Size + FontBytes.U16(record, endPointsEnd);
        float[]? rentedCoordinates = null;
        byte[]? rentedFlags = null;
        var coordinates = pointCount <= StackPoints
            ? stackalloc float[StackPoints * Axes]
            : (rentedCoordinates = ArrayPool<float>.Shared.Rent(pointCount * Axes));
        var flags = pointCount <= StackPoints ? stackalloc byte[StackPoints] : (rentedFlags = ArrayPool<byte>.Shared.Rent(pointCount));
        try
        {
            var points = new GlyfPoints(coordinates[..pointCount], coordinates.Slice(pointCount, pointCount), flags[..pointCount]);
            if (ReadPoints(record, flagsOffset, points))
            {
                Transform(points, transform);
                EmitContours(record, contourCount, points, ref sink);
            }
        }
        finally
        {
            if (rentedCoordinates is not null)
            {
                ArrayPool<float>.Shared.Return(rentedCoordinates);
            }

            if (rentedFlags is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedFlags);
            }
        }
    }

    /// <summary>Reads the flags and coordinates.</summary>
    /// <param name="record">The glyph record.</param>
    /// <param name="offset">The offset of the flags.</param>
    /// <param name="points">Receives the points.</param>
    /// <returns><see langword="false"/> when the record is truncated.</returns>
    private static bool ReadPoints(ReadOnlySpan<byte> record, int offset, GlyfPoints points) => ReadFlags(record, ref offset, points.Flags)
            && ReadCoordinates(record, ref offset, points.Flags, points.X, XShort, XSameOrPositive)
            && ReadCoordinates(record, ref offset, points.Flags, points.Y, YShort, YSameOrPositive);

    /// <summary>Reads the point flags, expanding repeats.</summary>
    /// <param name="record">The glyph record.</param>
    /// <param name="offset">The read position.</param>
    /// <param name="flags">Receives one flag per point.</param>
    /// <returns><see langword="false"/> when the record is truncated.</returns>
    private static bool ReadFlags(ReadOnlySpan<byte> record, ref int offset, Span<byte> flags)
    {
        for (var index = 0; index < flags.Length; index += FillFlag(record, ref offset, flags[index..]))
        {
            if (offset >= record.Length)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads one flag and its repeat count, and fills the points it covers.</summary>
    /// <param name="record">The glyph record.</param>
    /// <param name="offset">The read position, at the flag.</param>
    /// <param name="flags">The flags still to fill.</param>
    /// <returns>The number of points filled.</returns>
    private static int FillFlag(ReadOnlySpan<byte> record, ref int offset, Span<byte> flags)
    {
        var flag = record[offset];
        offset++;
        var count = 1;
        if ((flag & Repeat) != 0 && offset < record.Length)
        {
            count += record[offset];
            offset++;
        }

        count = Math.Min(count, flags.Length);
        flags[..count].Fill(flag);
        return count;
    }

    /// <summary>Reads one axis of delta-encoded coordinates.</summary>
    /// <param name="record">The glyph record.</param>
    /// <param name="offset">The read position.</param>
    /// <param name="flags">The point flags.</param>
    /// <param name="values">Receives the absolute coordinates.</param>
    /// <param name="shortBit">The flag of a one-byte delta.</param>
    /// <param name="sameBit">The flag of a positive or unchanged delta.</param>
    /// <returns><see langword="false"/> when the record is truncated.</returns>
    private static bool ReadCoordinates(ReadOnlySpan<byte> record, ref int offset, ReadOnlySpan<byte> flags, Span<float> values, int shortBit, int sameBit)
    {
        var value = 0;
        for (var i = 0; i < flags.Length; i++)
        {
            var flag = flags[i];
            if ((flag & shortBit) != 0)
            {
                if (offset >= record.Length)
                {
                    return false;
                }

                value += (flag & sameBit) != 0 ? record[offset] : -record[offset];
                offset++;
            }
            else if ((flag & sameBit) == 0)
            {
                if (offset + FontBytes.U16Size > record.Length)
                {
                    return false;
                }

                value += FontBytes.S16(record, offset);
                offset += FontBytes.U16Size;
            }

            values[i] = value;
        }

        return true;
    }

    /// <summary>Applies a transform to every point.</summary>
    /// <param name="points">The points.</param>
    /// <param name="transform">The transform.</param>
    private static void Transform(GlyfPoints points, in FontMatrix transform)
    {
        if (transform == FontMatrix.Identity)
        {
            return;
        }

        for (var i = 0; i < points.X.Length; i++)
        {
            var x = points.X[i];
            var y = points.Y[i];
            points.X[i] = transform.TransformX(x, y);
            points.Y[i] = transform.TransformY(x, y);
        }
    }

    /// <summary>Emits every contour.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="record">The glyph record.</param>
    /// <param name="contourCount">The number of contours.</param>
    /// <param name="points">The points.</param>
    /// <param name="sink">The sink.</param>
    private static void EmitContours<TSink>(ReadOnlySpan<byte> record, int contourCount, scoped GlyfPoints points, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var start = 0;
        for (var contour = 0; contour < contourCount; contour++)
        {
            var end = FontBytes.U16(record, HeaderSize + (contour * FontBytes.U16Size));
            if (end >= points.X.Length || end < start - 1)
            {
                return;
            }

            if (end > start)
            {
                EmitContour(points.Slice(start, end - start + 1), ref sink);
            }

            start = end + 1;
        }
    }

    /// <summary>Emits one contour, inserting the implied on-curve points between consecutive off-curve points.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="points">The contour's points.</param>
    /// <param name="sink">The sink.</param>
    private static void EmitContour<TSink>(scoped GlyfPoints points, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var last = points.X.Length - 1;
        var first = 0;
        var count = points.X.Length;
        float startX;
        float startY;
        if (points.IsOn(0))
        {
            startX = points.X[0];
            startY = points.Y[0];
            first = 1;
            count = last;
        }
        else if (points.IsOn(last))
        {
            startX = points.X[last];
            startY = points.Y[last];
            count = last;
        }
        else
        {
            startX = (points.X[0] + points.X[last]) * Half;
            startY = (points.Y[0] + points.Y[last]) * Half;
        }

        sink.MoveTo(startX, startY);
        var curve = default(QuadraticRun);
        for (var i = first; i < first + count; i++)
        {
            curve.Add(points.X[i], points.Y[i], points.IsOn(i), ref sink);
        }

        curve.Finish(startX, startY, ref sink);
        sink.Close();
    }

    /// <summary>The points of a glyph, held as parallel spans.</summary>
    /// <param name="x">The x coordinates.</param>
    /// <param name="y">The y coordinates.</param>
    /// <param name="flags">The flags.</param>
    private readonly ref struct GlyfPoints(Span<float> x, Span<float> y, Span<byte> flags)
    {
        /// <summary>The flag of an on-curve point.</summary>
        private const int OnCurve = 0x01;

        /// <summary>Gets the x coordinates.</summary>
        internal Span<float> X { get; } = x;

        /// <summary>Gets the y coordinates.</summary>
        internal Span<float> Y { get; } = y;

        /// <summary>Gets the flags.</summary>
        internal Span<byte> Flags { get; } = flags;

        /// <summary>Determines whether a point is on the curve.</summary>
        /// <param name="index">The point.</param>
        /// <returns><see langword="true"/> when on the curve.</returns>
        internal bool IsOn(int index) => (Flags[index] & OnCurve) != 0;

        /// <summary>Takes some of the points.</summary>
        /// <param name="start">The first point.</param>
        /// <param name="length">The number of points.</param>
        /// <returns>The points.</returns>
        internal GlyfPoints Slice(int start, int length) => new(X.Slice(start, length), Y.Slice(start, length), Flags.Slice(start, length));
    }

    /// <summary>Tracks a pending off-curve control point while a contour is emitted.</summary>
    private struct QuadraticRun
    {
        /// <summary>Whether a control point is pending.</summary>
        private bool _pending;

        /// <summary>The pending control point's x.</summary>
        private float _controlX;

        /// <summary>The pending control point's y.</summary>
        private float _controlY;

        /// <summary>Adds a point.</summary>
        /// <typeparam name="TSink">The sink type.</typeparam>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="onCurve">Whether the point is on the curve.</param>
        /// <param name="sink">The sink.</param>
        internal void Add<TSink>(float x, float y, bool onCurve, ref TSink sink)
            where TSink : IGlyphOutlineSink, allows ref struct
        {
            if (onCurve)
            {
                Finish(x, y, ref sink);
                return;
            }

            if (_pending)
            {
                sink.QuadraticTo(_controlX, _controlY, (_controlX + x) * Half, (_controlY + y) * Half);
            }

            _controlX = x;
            _controlY = y;
            _pending = true;
        }

        /// <summary>Ends a segment at an on-curve point.</summary>
        /// <typeparam name="TSink">The sink type.</typeparam>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="sink">The sink.</param>
        internal void Finish<TSink>(float x, float y, ref TSink sink)
            where TSink : IGlyphOutlineSink, allows ref struct
        {
            if (_pending)
            {
                sink.QuadraticTo(_controlX, _controlY, x, y);
            }
            else
            {
                sink.LineTo(x, y);
            }

            _pending = false;
        }
    }
}
