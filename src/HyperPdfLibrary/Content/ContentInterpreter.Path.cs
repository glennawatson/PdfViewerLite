// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace HyperPdfLibrary.Content;

/// <content>Path construction, painting and clipping.</content>
internal sealed partial class ContentInterpreter
{
    /// <summary>The operand index of the second coordinate pair.</summary>
    private const int SecondPair = 2;

    /// <summary>The operand index of the third coordinate pair.</summary>
    private const int ThirdPair = 4;

    /// <summary>The operand index of the height of a rectangle.</summary>
    private const int RectangleHeight = 3;

    /// <summary>Handles <c>m</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpMoveTo(ContentInterpreter self, ref ContentReader reader) => self.PathMove(reader.Number(0), reader.Number(1));

    /// <summary>Handles <c>l</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpLineTo(ContentInterpreter self, ref ContentReader reader)
    {
        if (self._pathPoints > 0)
        {
            self.PathLine(reader.Number(0), reader.Number(1));
        }
    }

    /// <summary>Handles <c>c</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpCurveTo(ContentInterpreter self, ref ContentReader reader)
    {
        if (self._pathPoints > 0)
        {
            self.PathCurve(reader.Number(0), reader.Number(1), reader.Number(SecondPair), reader.Number(SecondPair + 1), reader.Number(ThirdPair), reader.Number(ThirdPair + 1));
        }
    }

    /// <summary>Handles <c>v</c>: the first control point is the current point.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpCurveToV(ContentInterpreter self, ref ContentReader reader)
    {
        if (self._pathPoints > 0)
        {
            self.PathCurve(self._current.X, self._current.Y, reader.Number(0), reader.Number(1), reader.Number(SecondPair), reader.Number(SecondPair + 1));
        }
    }

    /// <summary>Handles <c>y</c>: the second control point is the end point.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpCurveToY(ContentInterpreter self, ref ContentReader reader)
    {
        if (self._pathPoints > 0)
        {
            self.PathCurve(reader.Number(0), reader.Number(1), reader.Number(SecondPair), reader.Number(SecondPair + 1), reader.Number(SecondPair), reader.Number(SecondPair + 1));
        }
    }

    /// <summary>Handles <c>h</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpClosePath(ContentInterpreter self, ref ContentReader reader)
    {
        if (self._pathPoints > 0)
        {
            self._path.Close();
        }
    }

    /// <summary>Handles <c>re</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpRectangle(ContentInterpreter self, ref ContentReader reader)
    {
        var x = reader.Number(0);
        var y = reader.Number(1);
        var width = reader.Number(SecondPair);
        var height = reader.Number(RectangleHeight);
        self.PathMove(x, y);
        self.PathLine(x + width, y);
        self.PathLine(x + width, y + height);
        self.PathLine(x, y + height);
        self._path.Close();
    }

    /// <summary>Handles <c>f</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpFill(ContentInterpreter self, ref ContentReader reader) => self.PaintPath(true, false, false, false);

    /// <summary>Handles <c>f*</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpFillEvenOdd(ContentInterpreter self, ref ContentReader reader) => self.PaintPath(true, false, true, false);

    /// <summary>Handles <c>S</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpStroke(ContentInterpreter self, ref ContentReader reader) => self.PaintPath(false, true, false, false);

    /// <summary>Handles <c>s</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpCloseStroke(ContentInterpreter self, ref ContentReader reader) => self.PaintPath(false, true, false, true);

    /// <summary>Handles <c>B</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpFillStroke(ContentInterpreter self, ref ContentReader reader) => self.PaintPath(true, true, false, false);

    /// <summary>Handles <c>B*</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpFillStrokeEvenOdd(ContentInterpreter self, ref ContentReader reader) => self.PaintPath(true, true, true, false);

    /// <summary>Handles <c>b</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpCloseFillStroke(ContentInterpreter self, ref ContentReader reader) => self.PaintPath(true, true, false, true);

    /// <summary>Handles <c>b*</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpCloseFillStrokeEvenOdd(ContentInterpreter self, ref ContentReader reader) => self.PaintPath(true, true, true, true);

    /// <summary>Handles <c>n</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpEndPath(ContentInterpreter self, ref ContentReader reader) => self.PaintPath(false, false, false, false);

    /// <summary>Handles <c>W</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpClip(ContentInterpreter self, ref ContentReader reader)
    {
        self._pendingClip = true;
        self._pendingClipEvenOdd = false;
    }

    /// <summary>Handles <c>W*</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpClipEvenOdd(ContentInterpreter self, ref ContentReader reader)
    {
        self._pendingClip = true;
        self._pendingClipEvenOdd = true;
    }

    /// <summary>Starts a subpath.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    private void PathMove(float x, float y)
    {
        _path.MoveTo(x, y);
        _current = new(x, y);
        _pathPoints++;
    }

    /// <summary>Adds a line.</summary>
    /// <param name="x">The end x.</param>
    /// <param name="y">The end y.</param>
    private void PathLine(float x, float y)
    {
        _path.LineTo(x, y);
        _current = new(x, y);
        _pathPoints++;
    }

    /// <summary>Adds a cubic curve.</summary>
    /// <param name="x1">The first control point x.</param>
    /// <param name="y1">The first control point y.</param>
    /// <param name="x2">The second control point x.</param>
    /// <param name="y2">The second control point y.</param>
    /// <param name="x3">The end x.</param>
    /// <param name="y3">The end y.</param>
    private void PathCurve(float x1, float y1, float x2, float y2, float x3, float y3)
    {
        _path.CubicTo(x1, y1, x2, y2, x3, y3);
        _current = new(x3, y3);
        _pathPoints++;
    }

    /// <summary>Paints the current path, applies a pending clip, and starts a new path.</summary>
    /// <param name="fill">Whether to fill.</param>
    /// <param name="stroke">Whether to stroke.</param>
    /// <param name="evenOdd">Whether the fill uses the even-odd rule.</param>
    /// <param name="close">Whether to close the path first.</param>
    private void PaintPath(bool fill, bool stroke, bool evenOdd, bool close)
    {
        if (_pathPoints == 0)
        {
            _pendingClip = false;
            return;
        }

        if (close)
        {
            _path.Close();
        }

        using var path = _path.Detach();
        _pathPoints = 0;
        if (_hidden == 0)
        {
            FillAndStroke(path, fill, stroke, evenOdd);
        }

        if (_pendingClip)
        {
            _device.Clip(path, _pendingClipEvenOdd, _state.Ctm);
        }

        _pendingClip = false;
    }

    /// <summary>Fills and strokes a path unless the colour paints nothing.</summary>
    /// <param name="path">The path.</param>
    /// <param name="fill">Whether to fill.</param>
    /// <param name="stroke">Whether to stroke.</param>
    /// <param name="evenOdd">Whether the fill uses the even-odd rule.</param>
    private void FillAndStroke(SKPath path, bool fill, bool stroke, bool evenOdd)
    {
        if (fill && stroke && NeedsStrokeKnockout())
        {
            FillAndStrokeKnockout(path, evenOdd);
            return;
        }

        if (fill && !_state.Fill.PaintsNothing)
        {
            _device.Fill(path, evenOdd, ref _state);
        }

        if (stroke && !_state.Stroke.PaintsNothing)
        {
            _device.Stroke(path, ref _state);
        }
    }

    /// <summary>Determines whether a fill and stroke pair must be drawn as a knockout group: both paint and the stroke is translucent.</summary>
    /// <returns><see langword="true"/> when the stroke must replace the fill under it.</returns>
    private bool NeedsStrokeKnockout() =>
        _state.StrokeAlpha < 1 && _state.FillAlpha > 0 && !_state.Fill.PaintsNothing && !_state.Stroke.PaintsNothing;

    /// <summary>
    /// Fills and strokes a path whose stroke is translucent. As in PDFium, the pair is one knockout group: the stroke
    /// replaces the fill under it instead of showing it through, and the group takes the state's blend mode and soft mask.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="evenOdd">Whether the fill uses the even-odd rule.</param>
    private void FillAndStrokeKnockout(SKPath path, bool evenOdd)
    {
        var reach = (_state.LineWidth * Math.Max(1, _state.MiterLimit)) + 1;
        var local = path.Bounds;
        local.Inflate(reach, reach);
        var bounds = SkiaConversions.ToSkMatrix(_state.Ctm).MapRect(local);
        var group = new GroupInfo(true, true, 1, _state.BlendMode, _state.SoftMask, bounds);
        var inner = _state;
        inner.BlendMode = PdfBlendMode.Normal;
        inner.SoftMask = null;
        _device.BeginGroup(group);
        _device.Fill(path, evenOdd, ref inner);
        _device.Stroke(path, ref inner);
        _device.EndGroup(group);
    }
}
