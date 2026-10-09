// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>Gathers, orders and filters the text runs of a page.</summary>
internal static class TextRunCollection
{
    /// <summary>The smallest width or height PDFium treats as non-zero.</summary>
    internal const float SizeEpsilon = 0.01F;

    /// <summary>The number of earlier runs checked for a duplicate of a run.</summary>
    private const int DuplicateLookBack = 5;

    /// <summary>The share of the text extent that must be covered for lines to count as horizontal.</summary>
    private const float HorizontalCoverage = 0.8F;

    /// <summary>The share of a glyph's width two duplicate runs may be apart horizontally.</summary>
    private const float DuplicateWidthShare = 0.9F;

    /// <summary>The fraction of a run's size two duplicate runs may be apart vertically, as a divisor.</summary>
    private const float DuplicateHeightDivisor = 8;

    /// <summary>How far from the end PDFium samples a character's width to compare empty runs.</summary>
    private const int SpacingSampleOffset = 2;

    /// <summary>The line heights the text must span before its extent decides the line direction.</summary>
    private const float LinesForDirection = 2;

    /// <summary>Half, for the width difference allowed between duplicate runs.</summary>
    private const float Half = 0.5F;

    /// <summary>Runs PDFium's ProcessObject over the runs.</summary>
    /// <param name="state">The reusable build state.</param>
    internal static void ProcessRuns(TextPageBuildState state)
    {
        if (state.Runs.Count == 0)
        {
            return;
        }

        state.LineDirection = FindLineDirection(state);
        for (var i = 0; i < state.Runs.Count; i++)
        {
            GatherRun(state, i);
        }

        TextInsertionAssembly.ProcessLine(state);
        state.Line.Clear();
        TextLineAssembly.CloseTempLine(state);
    }

    /// <summary>Decides the line direction from the coverage marks.</summary>
    /// <param name="horizontal">The column marks.</param>
    /// <param name="vertical">The row marks.</param>
    /// <param name="extent">The covered extent.</param>
    /// <param name="lineHeight">The height of the first run.</param>
    /// <returns>The direction.</returns>
    private static TextOrientation Orientation(bool[] horizontal, bool[] vertical, in PdfRectangle extent, float lineHeight)
    {
        var doubleLine = (int)(LinesForDirection * lineHeight);
        if (extent.Top - extent.Bottom < doubleLine)
        {
            return TextOrientation.Horizontal;
        }

        if (extent.Right - extent.Left < doubleLine)
        {
            return TextOrientation.Vertical;
        }

        var sumH = Filled(horizontal, (int)extent.Left, (int)extent.Right);
        if (sumH > HorizontalCoverage)
        {
            return TextOrientation.Horizontal;
        }

        var sumV = Filled(vertical, (int)extent.Bottom, (int)extent.Top);
        if (sumH > sumV)
        {
            return TextOrientation.Horizontal;
        }

        return sumH < sumV ? TextOrientation.Vertical : TextOrientation.Unknown;
    }

    /// <summary>Gets the share of marks set in a range.</summary>
    /// <param name="marks">The marks.</param>
    /// <param name="start">The first index.</param>
    /// <param name="end">The index after the last.</param>
    /// <returns>The share, 0 to 1.</returns>
    private static float Filled(bool[] marks, int start, int end)
    {
        if (start >= end)
        {
            return 0;
        }

        var count = marks.AsSpan(start, end - start).Count(true);
        return (float)count / (end - start);
    }

    /// <summary>Adds a run to the line being gathered, closing the line when the run starts another, as PDFium's ProcessTextObject does.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="index">The run.</param>
    private static void GatherRun(TextPageBuildState state, int index)
    {
        var run = state.Runs[index];
        if (MathF.Abs(run.Rect.Width) < SizeEpsilon)
        {
            return;
        }

        if (state.Line.Count == 0)
        {
            state.Line.Add(index);
            return;
        }

        if (IsDuplicateOfEarlier(state, index))
        {
            return;
        }

        var previous = state.Runs[state.Line[^1]];
        var previousWidth = TextGeometry.TransformDistance(previous.Matrix, MathF.Abs(GlyphWidth(state, previous, previous.GlyphCount - 1)));
        var thisWidth = TextGeometry.TransformDistance(run.Matrix, MathF.Abs(GlyphWidth(state, run, 0)));
        var threshold = MathF.Max(previousWidth, thisWidth) / TextInsertionAssembly.LineThresholdDivisor;
        var previousPosition = TextGeometry.Transform(state.Display, previous.Position);
        var position = TextGeometry.Transform(state.Display, run.Position);
        if (MathF.Abs(position.Y - previousPosition.Y) > threshold * TextInsertionAssembly.LineAbove)
        {
            TextInsertionAssembly.ProcessLine(state);
            state.Line.Clear();
            state.Line.Add(index);
            return;
        }

        InsertByX(state, index, position.X);
    }

    /// <summary>Inserts a run into the line after the last run that starts left of it.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="index">The run.</param>
    /// <param name="x">The run's x in viewer space.</param>
    private static void InsertByX(TextPageBuildState state, int index, float x)
    {
        for (var i = state.Line.Count; i > 0; i--)
        {
            if (x < TextGeometry.Transform(state.Display, state.Runs[state.Line[i - 1]].Position).X)
            {
                continue;
            }

            state.Line.Insert(i, index);
            return;
        }

        state.Line.Insert(0, index);
    }

    /// <summary>Gets a glyph's advance scaled by the font size, using PDFium's width fallbacks.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="run">The run.</param>
    /// <param name="glyph">The glyph index within the run.</param>
    /// <returns>The width.</returns>
    private static float GlyphWidth(TextPageBuildState state, in TextRun run, int glyph) => state.Glyphs[run.FirstGlyph + glyph].WidthUnits * run.FontSize / TextGlyphAssembly.GlyphUnits;

    /// <summary>Determines whether a run repeats one of the few runs before it, as fake bold and shadow text do.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="index">The run.</param>
    /// <returns><see langword="true"/> when the run is a duplicate.</returns>
    private static bool IsDuplicateOfEarlier(TextPageBuildState state, int index)
    {
        var current = state.Runs[index];
        var checkedRuns = 0;
        for (var i = index - 1; i >= 0 && checkedRuns < DuplicateLookBack; i--)
        {
            if (IsSameRun(state, state.Runs[i], current))
            {
                return true;
            }

            checkedRuns++;
        }

        return false;
    }

    /// <summary>Determines whether two runs show the same codes at nearly the same place, as PDFium's IsSameTextObject does.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="first">The earlier run.</param>
    /// <param name="second">The later run.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool IsSameRun(TextPageBuildState state, in TextRun first, in TextRun second)
    {
        var secondRect = second.Rect;
        if (!SameRects(state, first, second, ref secondRect) || first.GlyphCount != second.GlyphCount)
        {
            return false;
        }

        var a = TextGlyphAssembly.GlyphsOf(state, first);
        var b = TextGlyphAssembly.GlyphsOf(state, second);
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i].Code != b[i].Code)
            {
                return false;
            }
        }

        var diff = first.Position - second.Position;
        var charSize = (float)b[^1].WidthUnits;
        var maxSize = MathF.Max(MathF.Max(secondRect.Height, secondRect.Width), second.FontSize);
        return MathF.Abs(diff.X) <= DuplicateWidthShare * charSize * second.FontSize / TextGlyphAssembly.GlyphUnits && MathF.Abs(diff.Y) <= maxSize / DuplicateHeightDivisor;
    }

    /// <summary>Compares the bounds of two runs as PDFium's IsSameTextObject does.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="first">The earlier run.</param>
    /// <param name="second">The later run.</param>
    /// <param name="overlap">The later run's bounds; receives their overlap.</param>
    /// <returns><see langword="false"/> when the bounds rule the runs out.</returns>
    private static bool SameRects(TextPageBuildState state, in TextRun first, in TextRun second, ref PdfRectangle overlap)
    {
        var current = first.Rect;
        var bothEmpty = TextGeometry.IsEmpty(overlap) && TextGeometry.IsEmpty(current);
        if (bothEmpty)
        {
            return state.Chars.Count < SpacingSampleOffset || MathF.Abs(overlap.Left - current.Left) <= state.Chars[^SpacingSampleOffset].Box.Width;
        }

        overlap = TextGeometry.Intersect(overlap, current);
        return !TextGeometry.IsEmpty(overlap) && MathF.Abs(overlap.Width - current.Width) <= current.Width * Half && TextGeometry.Same(second.FontSize, first.FontSize);
    }

    /// <summary>Works out whether the page's lines run across or down it, as PDFium's FindTextlineFlowOrientation does.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <returns>The direction.</returns>
    private static TextOrientation FindLineDirection(TextPageBuildState state)
    {
        var width = (int)state.PageWidth;
        var height = (int)state.PageHeight;
        if (width <= 0 || height <= 0)
        {
            return TextOrientation.Unknown;
        }

        var horizontal = ArrayPool<bool>.Shared.Rent(width);
        var vertical = ArrayPool<bool>.Shared.Rent(height);
        try
        {
            Array.Clear(horizontal, 0, width);
            Array.Clear(vertical, 0, height);
            var extent = MarkCoverage(state, horizontal.AsSpan(0, width), vertical.AsSpan(0, height), out var lineHeight);
            return Orientation(horizontal, vertical, extent, lineHeight);
        }
        finally
        {
            ArrayPool<bool>.Shared.Return(horizontal);
            ArrayPool<bool>.Shared.Return(vertical);
        }
    }

    /// <summary>Marks the page columns and rows each run covers.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="horizontal">The column marks.</param>
    /// <param name="vertical">The row marks.</param>
    /// <param name="lineHeight">Receives the height of the first run with an area.</param>
    /// <returns>The covered extent: start and end column, start and end row.</returns>
    private static PdfRectangle MarkCoverage(TextPageBuildState state, Span<bool> horizontal, Span<bool> vertical, out float lineHeight)
    {
        lineHeight = 0;
        var extent = new PdfRectangle(horizontal.Length, vertical.Length, 0, 0);
        foreach (var run in CollectionsMarshal.AsSpan(state.Runs))
        {
            var rect = run.Rect;
            var minH = (int)Math.Clamp(rect.Left, 0, horizontal.Length);
            var maxH = (int)Math.Clamp(rect.Right, 0, horizontal.Length);
            var minV = (int)Math.Clamp(rect.Bottom, 0, vertical.Length);
            var maxV = (int)Math.Clamp(rect.Top, 0, vertical.Length);
            if (minH >= maxH || minV >= maxV)
            {
                continue;
            }

            horizontal[minH..maxH].Fill(true);
            vertical[minV..maxV].Fill(true);
            extent = new(Math.Min(extent.Left, minH), Math.Min(extent.Bottom, minV), Math.Max(extent.Right, maxH), Math.Max(extent.Top, maxV));
            if (lineHeight <= 0)
            {
                lineHeight = rect.Height;
            }
        }

        return extent;
    }
}
