// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;

namespace HyperPdfLibrary.PageObjects;

/// <content>The graphics state and path operators.</content>
internal sealed partial class PageContentParser
{
    /// <summary>The offset where the path being built starts, or -1 while there is none.</summary>
    private int _pathStart = -1;

    /// <summary>The clip the path being built asks for when it is painted.</summary>
    private PdfClipMode _pendingClip;

    /// <summary>The current point.</summary>
    private Vector2 _current;

    /// <summary>The start of the current subpath.</summary>
    private Vector2 _subpathStart;

    /// <summary>Handles <c>q</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpSave(PageContentParser self, ref ContentReader reader) => self._stack.Add(self._state);

    /// <summary>Handles <c>Q</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpRestore(PageContentParser self, ref ContentReader reader)
    {
        if (self._stack.Count == 0)
        {
            self._underflow++;
            return;
        }

        var last = self._stack.Count - 1;
        self._state = self._stack[last];
        self._stack.RemoveAt(last);
    }

    /// <summary>Handles <c>cm</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpConcatMatrix(PageContentParser self, ref ContentReader reader)
    {
        var matrix = new Matrix3x2(reader.Number(0), reader.Number(1), reader.Number(ThirdOperand), reader.Number(FourthOperand), reader.Number(FifthOperand), reader.Number(SixthOperand));
        self._state.Ctm = matrix * self._state.Ctm;
    }

    /// <summary>Handles <c>w</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpSetLineWidth(PageContentParser self, ref ContentReader reader) => self._state.LineWidth = Math.Max(0, reader.Number(0));

    /// <summary>Handles <c>m</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpMoveTo(PageContentParser self, ref ContentReader reader) =>
        self.AddSegment(new(PdfPathSegmentKind.MoveTo, reader.Number(0), reader.Number(1), 0, 0, 0, 0));

    /// <summary>Handles <c>l</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpLineTo(PageContentParser self, ref ContentReader reader) =>
        self.AddSegment(new(PdfPathSegmentKind.LineTo, reader.Number(0), reader.Number(1), 0, 0, 0, 0));

    /// <summary>Handles <c>c</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpCurveTo(PageContentParser self, ref ContentReader reader)
    {
        var x1 = reader.Number(0);
        var y1 = reader.Number(1);
        self.AddSegment(new(PdfPathSegmentKind.CurveTo, x1, y1, reader.Number(ThirdOperand), reader.Number(FourthOperand), reader.Number(FifthOperand), reader.Number(SixthOperand)));
    }

    /// <summary>Handles <c>v</c>: the first control point is the current point.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpCurveToV(PageContentParser self, ref ContentReader reader) =>
        self.AddSegment(new(PdfPathSegmentKind.CurveTo, self._current.X, self._current.Y, reader.Number(0), reader.Number(1), reader.Number(ThirdOperand), reader.Number(FourthOperand)));

    /// <summary>Handles <c>y</c>: the second control point is the end point.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpCurveToY(PageContentParser self, ref ContentReader reader)
    {
        var endX = reader.Number(ThirdOperand);
        var endY = reader.Number(FourthOperand);
        self.AddSegment(new(PdfPathSegmentKind.CurveTo, reader.Number(0), reader.Number(1), endX, endY, endX, endY));
    }

    /// <summary>Handles <c>h</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpClosePath(PageContentParser self, ref ContentReader reader) => self.AddSegment(new(PdfPathSegmentKind.Close, 0, 0, 0, 0, 0, 0));

    /// <summary>Handles <c>re</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpRectangle(PageContentParser self, ref ContentReader reader) =>
        self.AddSegment(new(PdfPathSegmentKind.Rectangle, reader.Number(0), reader.Number(1), reader.Number(ThirdOperand), reader.Number(FourthOperand), 0, 0));

    /// <summary>Handles <c>f</c> and <c>F</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpFill(PageContentParser self, ref ContentReader reader) => self.PaintPath(PdfPathPaintMode.Fill, false, false);

    /// <summary>Handles <c>f*</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpFillEvenOdd(PageContentParser self, ref ContentReader reader) => self.PaintPath(PdfPathPaintMode.Fill, true, false);

    /// <summary>Handles <c>S</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpStroke(PageContentParser self, ref ContentReader reader) => self.PaintPath(PdfPathPaintMode.Stroke, false, false);

    /// <summary>Handles <c>s</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpCloseStroke(PageContentParser self, ref ContentReader reader) => self.PaintPath(PdfPathPaintMode.Stroke, false, true);

    /// <summary>Handles <c>B</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpFillStroke(PageContentParser self, ref ContentReader reader) => self.PaintPath(PdfPathPaintMode.FillStroke, false, false);

    /// <summary>Handles <c>B*</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpFillStrokeEvenOdd(PageContentParser self, ref ContentReader reader) => self.PaintPath(PdfPathPaintMode.FillStroke, true, false);

    /// <summary>Handles <c>b</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpCloseFillStroke(PageContentParser self, ref ContentReader reader) => self.PaintPath(PdfPathPaintMode.FillStroke, false, true);

    /// <summary>Handles <c>b*</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpCloseFillStrokeEvenOdd(PageContentParser self, ref ContentReader reader) => self.PaintPath(PdfPathPaintMode.FillStroke, true, true);

    /// <summary>Handles <c>n</c>: ends the path, applying a pending clip.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpEndPath(PageContentParser self, ref ContentReader reader)
    {
        self.ApplyPendingClip();
        self.ResetPath();
    }

    /// <summary>Handles <c>W</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpClip(PageContentParser self, ref ContentReader reader) => self._pendingClip = PdfClipMode.NonZero;

    /// <summary>Handles <c>W*</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpClipEvenOdd(PageContentParser self, ref ContentReader reader) => self._pendingClip = PdfClipMode.EvenOdd;

    /// <summary>Adds a segment to the path being built.</summary>
    /// <param name="segment">The segment.</param>
    private void AddSegment(PdfPathSegment segment)
    {
        if (_pathStart < 0)
        {
            _pathStart = _operatorStart;
        }

        _segments.Add(segment);
        var point = new Vector2(segment.X1, segment.Y1);
        switch (segment.Kind)
        {
            case PdfPathSegmentKind.MoveTo or PdfPathSegmentKind.Rectangle:
            {
                _current = point;
                _subpathStart = point;
                break;
            }

            case PdfPathSegmentKind.LineTo:
            {
                _current = point;
                break;
            }

            case PdfPathSegmentKind.CurveTo:
            {
                _current = new(segment.X3, segment.Y3);
                break;
            }

            default:
            {
                _current = _subpathStart;
                break;
            }
        }
    }

    /// <summary>Paints the path as an object and applies its clip.</summary>
    /// <param name="mode">How the path is painted.</param>
    /// <param name="evenOdd">Whether the fill uses the even-odd rule.</param>
    /// <param name="close">Whether the operator closed the path first.</param>
    private void PaintPath(PdfPathPaintMode mode, bool evenOdd, bool close)
    {
        if (_pathStart < 0 || _segments.Count == 0)
        {
            ResetPath();
            return;
        }

        var segments = _segments.ToArray();
        var width = mode == PdfPathPaintMode.Fill ? 0 : _state.LineWidth;
        var item = new PdfPathObject { Segments = segments, PaintMode = mode, EvenOddFill = evenOdd, ClosesPath = close, Clip = _pendingClip };
        Add(item, PathBounds.Measure(segments, _state.Ctm, width), new(_pathStart, _operatorEnd));
        ApplyPendingClip();
        ResetPath();
    }

    /// <summary>Adds the pending clip to the state, after the path it came with was painted.</summary>
    private void ApplyPendingClip()
    {
        if (_pendingClip == PdfClipMode.None || _segments.Count == 0)
        {
            return;
        }

        var segments = _segments.ToArray();
        var bounds = PathBounds.Measure(segments, _state.Ctm, 0);
        var parent = _state.Clip;
        var clipped = parent is null ? bounds : parent.Bounds.Intersect(bounds);
        var path = new PdfClipPath(segments, _state.Ctm, _pendingClip == PdfClipMode.EvenOdd, bounds);
        _state.Clip = new(parent, path, clipped);
    }

    /// <summary>Forgets the path being built.</summary>
    private void ResetPath()
    {
        _segments.Clear();
        _pathStart = -1;
        _pendingClip = PdfClipMode.None;
    }
}
