// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Turns the runs a <see cref="TextDevice"/> collected into a <see cref="PdfTextPage"/>, following PDFium's
/// CPDF_TextPage: runs are grouped into lines and sorted, duplicates dropped, spaces, line breaks and hyphens inferred,
/// /ActualText applied and right-to-left runs put in logical order. One builder serves one thread and keeps its buffers.
/// </summary>
[DebuggerDisplay("TextPageBuilder: {_chars.Count} chars")]
internal sealed partial class TextPageBuilder
{
    /// <summary>The smallest width or height PDFium treats as non-zero.</summary>
    private const float SizeEpsilon = 0.01F;

    /// <summary>The font size used for characters without a run.</summary>
    private const float DefaultFontSize = 1;

    /// <summary>The glyph-space units in one text space unit.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>The number of earlier runs checked for a duplicate of a run.</summary>
    private const int DuplicateLookBack = 5;

    /// <summary>The share of the text extent that must be covered for lines to count as horizontal.</summary>
    private const float HorizontalCoverage = 0.8F;

    /// <summary>The character PDFium writes in the text for a glyph without Unicode, and for a joining hyphen.</summary>
    private const char NoText = (char)0xFFFE;

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

    /// <summary>The builder of each thread.</summary>
    [ThreadStatic]
    private static TextPageBuilder? _current;

    /// <summary>The characters of the page, in order.</summary>
    private readonly List<TextBuildChar> _chars = [];

    /// <summary>The characters of the line being built.</summary>
    private readonly List<TextBuildChar> _temp = [];

    /// <summary>The page text.</summary>
    private readonly List<char> _text = [];

    /// <summary>The text of the line being built, one character per entry of <see cref="_temp"/>.</summary>
    private readonly List<char> _tempText = [];

    /// <summary>The runs of the line being gathered, sorted by x.</summary>
    private readonly List<int> _line = [];

    /// <summary>The bidi segments of the line being closed.</summary>
    private readonly List<TextSegment> _segments = [];

    /// <summary>The code each font shows a space with, or -1.</summary>
    private readonly Dictionary<PdfFont, int> _spaceCodes = [];

    /// <summary>The runs, from <see cref="Device"/>.</summary>
    private readonly List<TextRun> _runs;

    /// <summary>The glyphs of the runs, from <see cref="Device"/>.</summary>
    private readonly List<TextGlyph> _glyphs;

    /// <summary>The Unicode text of the glyphs, from <see cref="Device"/>.</summary>
    private readonly List<char> _unicode;

    /// <summary>The page's user-to-viewer matrix, which PDFium calls the display matrix.</summary>
    private Matrix3x2 _display;

    /// <summary>The page width after rotation.</summary>
    private float _pageWidth;

    /// <summary>The page height after rotation.</summary>
    private float _pageHeight;

    /// <summary>Whether the document asks for right-to-left reading order.</summary>
    private bool _rightToLeft;

    /// <summary>The run whose characters were added last, or -1.</summary>
    private int _previous = -1;

    /// <summary>The direction most lines on the page flow in.</summary>
    private TextOrientation _lineDirection;

    /// <summary>The bounds of the current line's runs.</summary>
    private PdfRectangle _lineRect;

    /// <summary>Initializes a new instance of the <see cref="TextPageBuilder"/> class.</summary>
    internal TextPageBuilder()
    {
        _runs = Device.Runs;
        _glyphs = Device.Glyphs;
        _unicode = Device.Unicode;
    }

    /// <summary>Gets the builder for the calling thread.</summary>
    internal static TextPageBuilder Current => _current ??= new();

    /// <summary>Gets the device that collects the runs this builder reads.</summary>
    internal TextDevice Device { get; } = new();

    /// <summary>Builds a text page from the runs <see cref="Device"/> collected, then clears them.</summary>
    /// <param name="page">The page.</param>
    /// <param name="rightToLeft">Whether the document's viewer preferences ask for right-to-left order.</param>
    /// <returns>The text page.</returns>
    internal PdfTextPage Build(PdfPage page, bool rightToLeft)
    {
        _display = page.ViewerTransform;
        _pageWidth = page.Width;
        _pageHeight = page.Height;
        _rightToLeft = rightToLeft;
        try
        {
            ProcessRuns();
            return CreatePage(page);
        }
        finally
        {
            Clear();
        }
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

    /// <summary>Gets a run's glyphs.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The glyphs.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<TextGlyph> GlyphsOf(in TextRun run) => CollectionsMarshal.AsSpan(_glyphs).Slice(run.FirstGlyph, run.GlyphCount);

    /// <summary>Gets a glyph's Unicode text.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The text; empty when the font gives none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<char> TextOf(in TextGlyph glyph) => CollectionsMarshal.AsSpan(_unicode).Slice(glyph.UnicodeStart, glyph.UnicodeLength);

    /// <summary>Gets a glyph's first character, or its code when the font gives no text, as PDFium's insertion checks read it.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The character.</returns>
    private char FirstCharOf(in TextGlyph glyph) => glyph.UnicodeLength > 0 ? _unicode[glyph.UnicodeStart] : (char)glyph.Code;

    /// <summary>Runs PDFium's ProcessObject over the runs.</summary>
    private void ProcessRuns()
    {
        if (_runs.Count == 0)
        {
            return;
        }

        _lineDirection = FindLineDirection();
        for (var i = 0; i < _runs.Count; i++)
        {
            GatherRun(i);
        }

        ProcessLine();
        _line.Clear();
        CloseTempLine();
    }

    /// <summary>Adds a run to the line being gathered, closing the line when the run starts another, as PDFium's ProcessTextObject does.</summary>
    /// <param name="index">The run.</param>
    private void GatherRun(int index)
    {
        var run = _runs[index];
        if (MathF.Abs(run.Rect.Width) < SizeEpsilon)
        {
            return;
        }

        if (_line.Count == 0)
        {
            _line.Add(index);
            return;
        }

        if (IsDuplicateOfEarlier(index))
        {
            return;
        }

        var previous = _runs[_line[^1]];
        var previousWidth = TextGeometry.TransformDistance(previous.Matrix, MathF.Abs(GlyphWidth(previous, previous.GlyphCount - 1)));
        var thisWidth = TextGeometry.TransformDistance(run.Matrix, MathF.Abs(GlyphWidth(run, 0)));
        var threshold = MathF.Max(previousWidth, thisWidth) / LineThresholdDivisor;
        var previousPosition = TextGeometry.Transform(_display, previous.Position);
        var position = TextGeometry.Transform(_display, run.Position);
        if (MathF.Abs(position.Y - previousPosition.Y) > threshold * LineAbove)
        {
            ProcessLine();
            _line.Clear();
            _line.Add(index);
            return;
        }

        InsertByX(index, position.X);
    }

    /// <summary>Inserts a run into the line after the last run that starts left of it.</summary>
    /// <param name="index">The run.</param>
    /// <param name="x">The run's x in viewer space.</param>
    private void InsertByX(int index, float x)
    {
        for (var i = _line.Count; i > 0; i--)
        {
            if (x < TextGeometry.Transform(_display, _runs[_line[i - 1]].Position).X)
            {
                continue;
            }

            _line.Insert(i, index);
            return;
        }

        _line.Insert(0, index);
    }

    /// <summary>Gets a glyph's advance scaled by the font size, using PDFium's width fallbacks.</summary>
    /// <param name="run">The run.</param>
    /// <param name="glyph">The glyph index within the run.</param>
    /// <returns>The width.</returns>
    private float GlyphWidth(in TextRun run, int glyph) => _glyphs[run.FirstGlyph + glyph].WidthUnits * run.FontSize / GlyphUnits;

    /// <summary>Determines whether a run repeats one of the few runs before it, as fake bold and shadow text do.</summary>
    /// <param name="index">The run.</param>
    /// <returns><see langword="true"/> when the run is a duplicate.</returns>
    private bool IsDuplicateOfEarlier(int index)
    {
        var current = _runs[index];
        var checkedRuns = 0;
        for (var i = index - 1; i >= 0 && checkedRuns < DuplicateLookBack; i--)
        {
            if (IsSameRun(_runs[i], current))
            {
                return true;
            }

            checkedRuns++;
        }

        return false;
    }

    /// <summary>Determines whether two runs show the same codes at nearly the same place, as PDFium's IsSameTextObject does.</summary>
    /// <param name="first">The earlier run.</param>
    /// <param name="second">The later run.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private bool IsSameRun(in TextRun first, in TextRun second)
    {
        var secondRect = second.Rect;
        if (!SameRects(first, second, ref secondRect) || first.GlyphCount != second.GlyphCount)
        {
            return false;
        }

        var a = GlyphsOf(first);
        var b = GlyphsOf(second);
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
        return MathF.Abs(diff.X) <= DuplicateWidthShare * charSize * second.FontSize / GlyphUnits
            && MathF.Abs(diff.Y) <= maxSize / DuplicateHeightDivisor;
    }

    /// <summary>Compares the bounds of two runs as PDFium's IsSameTextObject does.</summary>
    /// <param name="first">The earlier run.</param>
    /// <param name="second">The later run.</param>
    /// <param name="overlap">The later run's bounds; receives their overlap.</param>
    /// <returns><see langword="false"/> when the bounds rule the runs out.</returns>
    private bool SameRects(in TextRun first, in TextRun second, ref PdfRectangle overlap)
    {
        var current = first.Rect;
        var bothEmpty = TextGeometry.IsEmpty(overlap) && TextGeometry.IsEmpty(current);
        if (bothEmpty)
        {
            return _chars.Count < SpacingSampleOffset || MathF.Abs(overlap.Left - current.Left) <= _chars[^SpacingSampleOffset].Box.Width;
        }

        overlap = TextGeometry.Intersect(overlap, current);
        return !TextGeometry.IsEmpty(overlap)
            && MathF.Abs(overlap.Width - current.Width) <= current.Width * Half
            && TextGeometry.Same(second.FontSize, first.FontSize);
    }

    /// <summary>Works out whether the page's lines run across or down it, as PDFium's FindTextlineFlowOrientation does.</summary>
    /// <returns>The direction.</returns>
    private TextOrientation FindLineDirection()
    {
        var width = (int)_pageWidth;
        var height = (int)_pageHeight;
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
            var extent = MarkCoverage(horizontal.AsSpan(0, width), vertical.AsSpan(0, height), out var lineHeight);
            return Orientation(horizontal, vertical, extent, lineHeight);
        }
        finally
        {
            ArrayPool<bool>.Shared.Return(horizontal);
            ArrayPool<bool>.Shared.Return(vertical);
        }
    }

    /// <summary>Marks the page columns and rows each run covers.</summary>
    /// <param name="horizontal">The column marks.</param>
    /// <param name="vertical">The row marks.</param>
    /// <param name="lineHeight">Receives the height of the first run with an area.</param>
    /// <returns>The covered extent: start and end column, start and end row.</returns>
    private PdfRectangle MarkCoverage(Span<bool> horizontal, Span<bool> vertical, out float lineHeight)
    {
        lineHeight = 0;
        var extent = new PdfRectangle(horizontal.Length, vertical.Length, 0, 0);
        foreach (var run in CollectionsMarshal.AsSpan(_runs))
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

    /// <summary>Drops references to the device's lists and resets the line state.</summary>
    private void Clear()
    {
        _chars.Clear();
        _temp.Clear();
        _text.Clear();
        _tempText.Clear();
        _line.Clear();
        _segments.Clear();
        _spaceCodes.Clear();
        Device.Clear();
        _previous = -1;
        _lineDirection = TextOrientation.Unknown;
        _lineRect = default;
    }
}
