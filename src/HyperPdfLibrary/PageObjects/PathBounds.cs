// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Measures where a path lies after a matrix, following curves exactly rather than by their control points.</summary>
internal static class PathBounds
{
    /// <summary>The number of corners of a rectangle.</summary>
    private const int RectangleCorners = 4;

    /// <summary>The number of axes, and of turning points a cubic has on one.</summary>
    private const int Axes = 2;

    /// <summary>Half, for the half width of a stroke.</summary>
    private const float Half = 0.5F;

    /// <summary>Two, a quadratic's coefficient.</summary>
    private const float Two = 2F;

    /// <summary>Three, a cubic's coefficient.</summary>
    private const float Three = 3F;

    /// <summary>Four, the discriminant's coefficient.</summary>
    private const float Four = 4F;

    /// <summary>The ratio of the segments that makes a cubic's extremum search stable.</summary>
    private const float QuadraticEpsilon = 1E-9F;

    /// <summary>Measures a path.</summary>
    /// <param name="segments">The path.</param>
    /// <param name="matrix">The matrix from the path's coordinates to user space.</param>
    /// <param name="strokeWidth">The stroke width in the path's coordinates, or 0 for a path that is not stroked.</param>
    /// <returns>The bounds in user space; empty for a path with no points.</returns>
    internal static PdfRectangle Measure(ReadOnlySpan<PdfPathSegment> segments, Matrix3x2 matrix, float strokeWidth)
    {
        var box = default(Extent);
        var current = Vector2.Zero;
        foreach (var segment in segments)
        {
            current = AddSegment(ref box, segment, matrix, current);
        }

        if (!box.Any)
        {
            return default;
        }

        var spread = strokeWidth > 0 ? strokeWidth * Half * Scale(matrix) : 0;
        return new(box.MinX - spread, box.MinY - spread, box.MaxX + spread, box.MaxY + spread);
    }

    /// <summary>Gets the average scale of a matrix.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The square root of the absolute determinant.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Scale(Matrix3x2 matrix) => MathF.Sqrt(MathF.Abs(matrix.GetDeterminant()));

    /// <summary>Adds one segment's points to the extent.</summary>
    /// <param name="box">The extent.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="matrix">The matrix.</param>
    /// <param name="current">The current point, in path coordinates.</param>
    /// <returns>The new current point.</returns>
    private static Vector2 AddSegment(ref Extent box, in PdfPathSegment segment, Matrix3x2 matrix, Vector2 current)
    {
        var first = new Vector2(segment.X1, segment.Y1);
        switch (segment.Kind)
        {
            case PdfPathSegmentKind.MoveTo or PdfPathSegmentKind.LineTo:
            {
                box.Add(Vector2.Transform(first, matrix));
                return first;
            }

            case PdfPathSegmentKind.CurveTo:
            {
                var end = new Vector2(segment.X3, segment.Y3);
                AddCurve(ref box, matrix, current, first, new(segment.X2, segment.Y2), end);
                return end;
            }

            case PdfPathSegmentKind.Rectangle:
            {
                AddRectangle(ref box, segment, matrix);
                return first;
            }

            default:
            {
                return current;
            }
        }
    }

    /// <summary>Adds a rectangle's corners.</summary>
    /// <param name="box">The extent.</param>
    /// <param name="segment">The rectangle segment.</param>
    /// <param name="matrix">The matrix.</param>
    private static void AddRectangle(ref Extent box, in PdfPathSegment segment, Matrix3x2 matrix)
    {
        Span<Vector2> corners =
        [
            new(segment.X1, segment.Y1),
            new(segment.X1 + segment.X2, segment.Y1),
            new(segment.X1 + segment.X2, segment.Y1 + segment.Y2),
            new(segment.X1, segment.Y1 + segment.Y2),
        ];
        for (var i = 0; i < RectangleCorners; i++)
        {
            box.Add(Vector2.Transform(corners[i], matrix));
        }
    }

    /// <summary>Adds a cubic curve: its end points and the points where it turns on either axis.</summary>
    /// <param name="box">The extent.</param>
    /// <param name="matrix">The matrix.</param>
    /// <param name="p0">The start.</param>
    /// <param name="p1">The first control point.</param>
    /// <param name="p2">The second control point.</param>
    /// <param name="p3">The end.</param>
    private static void AddCurve(ref Extent box, Matrix3x2 matrix, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        var a = Vector2.Transform(p0, matrix);
        var b = Vector2.Transform(p1, matrix);
        var c = Vector2.Transform(p2, matrix);
        var d = Vector2.Transform(p3, matrix);
        box.Add(a);
        box.Add(d);
        Span<float> roots = stackalloc float[Axes];
        for (var axis = 0; axis < Axes; axis++)
        {
            var count = TurningPoints(Component(a, axis), Component(b, axis), Component(c, axis), Component(d, axis), roots);
            for (var i = 0; i < count; i++)
            {
                box.Add(Point(a, b, c, d, roots[i]));
            }
        }
    }

    /// <summary>Gets one coordinate of a point.</summary>
    /// <param name="point">The point.</param>
    /// <param name="axis">0 for x, 1 for y.</param>
    /// <returns>The coordinate.</returns>
    private static float Component(Vector2 point, int axis) => axis == 0 ? point.X : point.Y;

    /// <summary>Finds where a cubic's derivative is zero inside (0, 1) on one axis.</summary>
    /// <param name="p0">The start coordinate.</param>
    /// <param name="p1">The first control coordinate.</param>
    /// <param name="p2">The second control coordinate.</param>
    /// <param name="p3">The end coordinate.</param>
    /// <param name="roots">Receives up to two parameters.</param>
    /// <returns>The number of parameters found.</returns>
    private static int TurningPoints(float p0, float p1, float p2, float p3, Span<float> roots)
    {
        // The derivative divided by three is a t^2 + b t + c.
        var a = -p0 + (Three * p1) - (Three * p2) + p3;
        var b = Two * (p0 - (Two * p1) + p2);
        var c = p1 - p0;
        var count = 0;
        if (MathF.Abs(a) < QuadraticEpsilon)
        {
            if (MathF.Abs(b) >= QuadraticEpsilon)
            {
                count = Keep(-c / b, roots, count);
            }

            return count;
        }

        var discriminant = (b * b) - (Four * a * c);
        if (discriminant < 0)
        {
            return 0;
        }

        var root = MathF.Sqrt(discriminant);
        count = Keep((-b + root) / (Two * a), roots, count);
        return Keep((-b - root) / (Two * a), roots, count);
    }

    /// <summary>Keeps a parameter that lies inside the curve.</summary>
    /// <param name="t">The parameter.</param>
    /// <param name="roots">The kept parameters.</param>
    /// <param name="count">The number kept so far.</param>
    /// <returns>The new count.</returns>
    private static int Keep(float t, Span<float> roots, int count)
    {
        if (!(t > 0 && t < 1) || count >= roots.Length)
        {
            return count;
        }

        roots[count] = t;
        return count + 1;
    }

    /// <summary>Evaluates a cubic Bezier curve.</summary>
    /// <param name="a">The start.</param>
    /// <param name="b">The first control point.</param>
    /// <param name="c">The second control point.</param>
    /// <param name="d">The end.</param>
    /// <param name="t">The parameter from 0 to 1.</param>
    /// <returns>The point.</returns>
    private static Vector2 Point(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        var u = 1 - t;
        return (a * (u * u * u)) + (b * (Three * u * u * t)) + (c * (Three * u * t * t)) + (d * (t * t * t));
    }

    /// <summary>The smallest rectangle around the points added.</summary>
    private record struct Extent
    {
        /// <summary>Gets or sets the smallest x.</summary>
        internal float MinX { get; set; }

        /// <summary>Gets or sets the smallest y.</summary>
        internal float MinY { get; set; }

        /// <summary>Gets or sets the largest x.</summary>
        internal float MaxX { get; set; }

        /// <summary>Gets or sets the largest y.</summary>
        internal float MaxY { get; set; }

        /// <summary>Gets or sets a value indicating whether any point was added.</summary>
        internal bool Any { get; set; }

        /// <summary>Adds a point.</summary>
        /// <param name="point">The point.</param>
        internal void Add(Vector2 point)
        {
            if (!Any)
            {
                MinX = point.X;
                MaxX = point.X;
                MinY = point.Y;
                MaxY = point.Y;
                Any = true;
                return;
            }

            MinX = MathF.Min(MinX, point.X);
            MaxX = MathF.Max(MaxX, point.X);
            MinY = MathF.Min(MinY, point.Y);
            MaxY = MathF.Max(MaxY, point.Y);
        }
    }
}
