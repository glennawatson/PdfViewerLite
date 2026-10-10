// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Drawing;

/// <summary>Builds a path from ordered drawing commands.</summary>
[DebuggerDisplay("PdfPathBuilder: {_commands.Count} commands")]
public sealed class PdfPathBuilder
{
    /// <summary>The ordered commands being built.</summary>
    private readonly List<PdfPathCommand> _commands = [];

    /// <summary>Gets or sets the rule used to fill paths created by this builder.</summary>
    public PdfPathFillRule FillRule { get; set; }

    /// <summary>Starts a contour at a point.</summary>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MoveTo(float x, float y) => _commands.Add(new(PdfPathCommandKind.MoveTo, new(x, y), default, default));

    /// <summary>Adds a line to a point.</summary>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LineTo(float x, float y) => _commands.Add(new(PdfPathCommandKind.LineTo, new(x, y), default, default));

    /// <summary>Adds a quadratic curve to a point.</summary>
    /// <param name="controlX">The horizontal coordinate of the control point.</param>
    /// <param name="controlY">The vertical coordinate of the control point.</param>
    /// <param name="x">The horizontal coordinate of the endpoint.</param>
    /// <param name="y">The vertical coordinate of the endpoint.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void QuadTo(float controlX, float controlY, float x, float y) =>
        _commands.Add(new(PdfPathCommandKind.QuadraticTo, new(x, y), new(controlX, controlY), default));

    /// <summary>Adds a cubic curve to a point.</summary>
    /// <param name="control1X">The horizontal coordinate of the first control point.</param>
    /// <param name="control1Y">The vertical coordinate of the first control point.</param>
    /// <param name="control2X">The horizontal coordinate of the second control point.</param>
    /// <param name="control2Y">The vertical coordinate of the second control point.</param>
    /// <param name="x">The horizontal coordinate of the endpoint.</param>
    /// <param name="y">The vertical coordinate of the endpoint.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CubicTo(float control1X, float control1Y, float control2X, float control2Y, float x, float y) =>
        _commands.Add(new(PdfPathCommandKind.CubicTo, new(x, y), new(control1X, control1Y), new(control2X, control2Y)));

    /// <summary>Closes the current contour.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Close() => _commands.Add(new(PdfPathCommandKind.Close, default, default, default));

    /// <summary>Clears all commands and restores the winding fill rule.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Reset()
    {
        _commands.Clear();
        FillRule = PdfPathFillRule.Winding;
    }

    /// <summary>Adds a rectangular contour.</summary>
    /// <param name="rect">The rectangle.</param>
    public void AddRect(PdfRect rect)
    {
        MoveTo(rect.Left, rect.Top);
        LineTo(rect.Right, rect.Top);
        LineTo(rect.Right, rect.Bottom);
        LineTo(rect.Left, rect.Bottom);
        Close();
    }

    /// <summary>Appends another path's commands.</summary>
    /// <param name="path">The path to append.</param>
    /// <exception cref="ArgumentNullException">The path is <see langword="null"/>.</exception>
    public void AddPath(PdfPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        foreach (var command in path.Commands)
        {
            _commands.Add(command);
        }
    }

    /// <summary>Appends another path after applying a transform to its points.</summary>
    /// <param name="path">The path to append.</param>
    /// <param name="matrix">The transform.</param>
    /// <exception cref="ArgumentNullException">The path is <see langword="null"/>.</exception>
    public void AddPath(PdfPath path, Matrix3x2 matrix)
    {
        ArgumentNullException.ThrowIfNull(path);
        foreach (var command in path.Commands)
        {
            _commands.Add(command.Transform(matrix));
        }
    }

    /// <summary>Transforms the points already added to this builder.</summary>
    /// <param name="matrix">The transform.</param>
    public void Transform(Matrix3x2 matrix)
    {
        for (var i = 0; i < _commands.Count; i++)
        {
            _commands[i] = _commands[i].Transform(matrix);
        }
    }

    /// <summary>Creates a path with an independent, exact-sized copy of the commands.</summary>
    /// <returns>The immutable path.</returns>
    public PdfPath Detach()
    {
        if (_commands.Count == 0)
        {
            return FillRule == PdfPathFillRule.Winding ? PdfPath.Empty : new([], FillRule);
        }

        var path = new PdfPath(_commands.ToArray(), FillRule);
        _commands.Clear();
        return path;
    }
}
