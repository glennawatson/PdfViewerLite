// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Drawing;

/// <summary>An immutable path made from managed drawing commands.</summary>
[DebuggerDisplay("PdfPath: {PointCount} points")]
public sealed class PdfPath
{
    /// <summary>The exact, immutable command array owned by this path.</summary>
    private readonly PdfPathCommand[] _commands;

    /// <summary>Initializes a new instance of the <see cref="PdfPath"/> class.</summary>
    /// <param name="commands">The immutable commands.</param>
    /// <param name="fillRule">The rule used to fill the path.</param>
    internal PdfPath(PdfPathCommand[] commands, PdfPathFillRule fillRule)
    {
        _commands = commands;
        FillRule = fillRule;
        var measurements = Measure(commands);
        Bounds = measurements.Bounds;
        PointCount = measurements.PointCount;
        IsEmpty = measurements.IsEmpty;
    }

    /// <summary>Gets an empty winding path.</summary>
    public static PdfPath Empty { get; } = new([], PdfPathFillRule.Winding);

    /// <summary>Gets the fill rule.</summary>
    public PdfPathFillRule FillRule { get; }

    /// <summary>Gets the bounds of the path's endpoints and control points.</summary>
    public PdfRect Bounds { get; }

    /// <summary>Gets the number of endpoints and curve control points.</summary>
    public int PointCount { get; }

    /// <summary>Gets whether the path has no line or curve segments.</summary>
    public bool IsEmpty { get; }

    /// <summary>Gets the path commands as a read-only view.</summary>
    public ReadOnlySpan<PdfPathCommand> Commands => _commands;

    /// <summary>Returns a copy of the path transformed by a matrix.</summary>
    /// <param name="matrix">The transform.</param>
    /// <returns>An independent path with transformed points.</returns>
    public PdfPath Transform(Matrix3x2 matrix)
    {
        if (_commands.Length == 0)
        {
            return FillRule == PdfPathFillRule.Winding ? Empty : new([], FillRule);
        }

        var transformed = new PdfPathCommand[_commands.Length];
        for (var i = 0; i < _commands.Length; i++)
        {
            transformed[i] = _commands[i].Transform(matrix);
        }

        return new(transformed, FillRule);
    }

    /// <summary>Calculates the bounds and point count for a command sequence.</summary>
    /// <param name="commands">The commands.</param>
    /// <returns>The path measurements.</returns>
    private static PathMeasurements Measure(ReadOnlySpan<PdfPathCommand> commands)
    {
        var left = float.PositiveInfinity;
        var top = float.PositiveInfinity;
        var right = float.NegativeInfinity;
        var bottom = float.NegativeInfinity;
        var pointCount = 0;
        var hasSegment = false;

        foreach (var command in commands)
        {
            pointCount += command.PointCount;
            hasSegment |= command.Kind is PdfPathCommandKind.LineTo or PdfPathCommandKind.QuadraticTo or PdfPathCommandKind.CubicTo;
            for (var i = 0; i < command.PointCount; i++)
            {
                var point = command.GetPoint(i);
                left = MathF.Min(left, point.X);
                top = MathF.Min(top, point.Y);
                right = MathF.Max(right, point.X);
                bottom = MathF.Max(bottom, point.Y);
            }
        }

        var bounds = pointCount == 0 ? PdfRect.Empty : new(left, top, right, bottom);
        return new(bounds, pointCount, !hasSegment);
    }

    /// <summary>The bounds and point count calculated from path commands.</summary>
    /// <param name="Bounds">The control-point bounds.</param>
    /// <param name="PointCount">The endpoints and control points.</param>
    /// <param name="IsEmpty">Whether the path contains no drawing segment.</param>
    private readonly record struct PathMeasurements(PdfRect Bounds, int PointCount, bool IsEmpty);
}
