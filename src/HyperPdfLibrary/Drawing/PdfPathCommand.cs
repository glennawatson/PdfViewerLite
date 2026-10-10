// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Drawing;

/// <summary>A move, line, curve or close operation in a path.</summary>
/// <param name="Kind">The operation.</param>
/// <param name="Point">The endpoint, or the moved point.</param>
/// <param name="Control1">The first curve control point.</param>
/// <param name="Control2">The second curve control point.</param>
[DebuggerDisplay("PdfPathCommand: {Kind}")]
public readonly record struct PdfPathCommand(PdfPathCommandKind Kind, PdfPoint Point, PdfPoint Control1, PdfPoint Control2)
{
    /// <summary>The points in a quadratic command.</summary>
    private const int QuadraticPointCount = 2;

    /// <summary>The points in a cubic command.</summary>
    private const int CubicPointCount = 3;

    /// <summary>The endpoint index in a cubic command.</summary>
    private const int CubicLastPointIndex = 2;

    /// <summary>Gets the number of points used by this operation.</summary>
    public int PointCount => Kind switch
    {
        PdfPathCommandKind.MoveTo or PdfPathCommandKind.LineTo => 1,
        PdfPathCommandKind.QuadraticTo => QuadraticPointCount,
        PdfPathCommandKind.CubicTo => CubicPointCount,
        _ => 0,
    };

    /// <summary>Transforms every point used by the operation.</summary>
    /// <param name="matrix">The transform.</param>
    /// <returns>The transformed operation.</returns>
    internal PdfPathCommand Transform(Matrix3x2 matrix) => Kind switch
    {
        PdfPathCommandKind.MoveTo or PdfPathCommandKind.LineTo => this with { Point = Transform(Point, matrix) },
        PdfPathCommandKind.QuadraticTo => this with
        {
            Point = Transform(Point, matrix),
            Control1 = Transform(Control1, matrix),
        },
        PdfPathCommandKind.CubicTo => this with
        {
            Point = Transform(Point, matrix),
            Control1 = Transform(Control1, matrix),
            Control2 = Transform(Control2, matrix),
        },
        _ => this,
    };

    /// <summary>Gets the point at the requested index.</summary>
    /// <param name="index">The point index.</param>
    /// <returns>The point.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside this command's points.</exception>
    internal PdfPoint GetPoint(int index) => Kind switch
    {
        PdfPathCommandKind.MoveTo or PdfPathCommandKind.LineTo when index == 0 => Point,
        PdfPathCommandKind.QuadraticTo when index == 0 => Control1,
        PdfPathCommandKind.QuadraticTo when index == 1 => Point,
        PdfPathCommandKind.CubicTo when index == 0 => Control1,
        PdfPathCommandKind.CubicTo when index == 1 => Control2,
        PdfPathCommandKind.CubicTo when index == CubicLastPointIndex => Point,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    /// <summary>Transforms a point.</summary>
    /// <param name="point">The point.</param>
    /// <param name="matrix">The transform.</param>
    /// <returns>The transformed point.</returns>
    private static PdfPoint Transform(PdfPoint point, Matrix3x2 matrix)
    {
        var transformed = Vector2.Transform(new(point.X, point.Y), matrix);
        return new(transformed.X, transformed.Y);
    }
}
