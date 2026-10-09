// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Works out a reading order from geometry for pages without usable tags: items form lines, lines split at column
/// gaps, the page is cut recursively at its widest empty band (columns left to right, bands top to bottom), and each
/// region's lines group into paragraphs. Text much larger than the page's body text becomes a heading. Every node is
/// labelled with the origin it was given, so readers know the order is inferred.
/// </summary>
[DebuggerDisplay("InferredLayoutBuilder: page {_pageIndex}, {_items.Count} items")]
internal sealed class InferredLayoutBuilder
{
    /// <summary>How far, as a share of height, two items' centres may differ and still sit on one line.</summary>
    private const float LineTolerance = 0.5F;

    /// <summary>The gap inside a line, in line heights, that separates two columns.</summary>
    private const float ColumnGapInLine = 1.2F;

    /// <summary>The least empty vertical band, in body text heights, that separates columns.</summary>
    private const float MinColumnGap = 1F;

    /// <summary>The least empty horizontal band, in body text heights, that separates blocks.</summary>
    private const float MinBandGap = 1F;

    /// <summary>The gap between lines, in line heights, that starts a new paragraph.</summary>
    private const float ParagraphGap = 0.6F;

    /// <summary>The change in line height that starts a new paragraph.</summary>
    private const float SizeChange = 1.25F;

    /// <summary>The share of body text height a heading's lines reach.</summary>
    private const float HeadingRatio = 1.3F;

    /// <summary>The share of body text height a level 1 heading's lines reach.</summary>
    private const float TopHeadingRatio = 1.8F;

    /// <summary>The most lines a heading has.</summary>
    private const int MaxHeadingLines = 3;

    /// <summary>The deepest recursive cut.</summary>
    private const int MaxCutDepth = 32;

    /// <summary>The gap between items, in line heights, that separates words.</summary>
    private const float WordGap = 0.15F;

    /// <summary>The level of a smaller inferred heading.</summary>
    private const int SecondLevel = 2;

    /// <summary>The divisor that finds a middle.</summary>
    private const int Halves = 2;

    /// <summary>Where the nodes come from.</summary>
    private readonly PdfNodeOrigin _origin;

    /// <summary>The zero based page index.</summary>
    private readonly int _pageIndex;

    /// <summary>The items' text, back to back.</summary>
    private readonly string _text;

    /// <summary>The items.</summary>
    private readonly List<LayoutItem> _items;

    /// <summary>Builds node text.</summary>
    private readonly StringBuilder _builder = new();

    /// <summary>The median item height: the body text size.</summary>
    private float _bodyHeight;

    /// <summary>Initializes a new instance of the <see cref="InferredLayoutBuilder"/> class.</summary>
    /// <param name="origin">Where the nodes come from.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="text">The items' text, back to back.</param>
    /// <param name="items">The items.</param>
    internal InferredLayoutBuilder(PdfNodeOrigin origin, int pageIndex, string text, List<LayoutItem> items)
    {
        _origin = origin;
        _pageIndex = pageIndex;
        _text = text;
        _items = items;
    }

    /// <summary>Builds the nodes in reading order.</summary>
    /// <param name="output">Receives the nodes.</param>
    internal void Build(List<PdfSemanticNode> output)
    {
        if (_items.Count == 0)
        {
            return;
        }

        _bodyHeight = MedianHeight(_items);
        var lines = BuildLines();
        var regions = new List<List<LayoutLine>>();
        Cut(lines, 0, regions);
        foreach (var region in regions)
        {
            AddParagraphs(region, output);
        }
    }

    /// <summary>Gets the median item height.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The height; at least a small positive number.</returns>
    private static float MedianHeight(List<LayoutItem> items)
    {
        var heights = new float[items.Count];
        for (var i = 0; i < heights.Length; i++)
        {
            heights[i] = items[i].Bounds.Height;
        }

        Array.Sort(heights);
        return Math.Max(heights[heights.Length / Halves], float.Epsilon);
    }

    /// <summary>Finds the widest empty band across a region, along one axis.</summary>
    /// <param name="region">The lines.</param>
    /// <param name="alongX">Whether to look for a vertical band (between columns) rather than a horizontal one.</param>
    /// <returns>The band, or one of zero width.</returns>
    private static LayoutGap FindGap(List<LayoutLine> region, bool alongX)
    {
        var starts = new float[region.Count];
        var ends = new float[region.Count];
        for (var i = 0; i < region.Count; i++)
        {
            var bounds = region[i].Bounds;
            starts[i] = alongX ? bounds.Left : bounds.Top;
            ends[i] = alongX ? bounds.Right : bounds.Bottom;
        }

        Array.Sort(starts, ends);
        var best = default(LayoutGap);
        var reach = ends[0];
        for (var i = 1; i < starts.Length; i++)
        {
            var width = starts[i] - reach;
            if (width > best.Width)
            {
                best = new((reach + starts[i]) / Halves, width);
            }

            reach = Math.Max(reach, ends[i]);
        }

        return best;
    }

    /// <summary>Sorts lines top to bottom, then left to right.</summary>
    /// <param name="a">The first line.</param>
    /// <param name="b">The second line.</param>
    /// <returns>The order.</returns>
    private static int CompareTopDown(LayoutLine a, LayoutLine b)
    {
        var top = a.Bounds.Top.CompareTo(b.Bounds.Top);
        return top != 0 ? top : a.Bounds.Left.CompareTo(b.Bounds.Left);
    }

    /// <summary>Determines whether an item sits on a line.</summary>
    /// <param name="row">The line.</param>
    /// <param name="item">The item.</param>
    /// <returns><see langword="true"/> when their centres are close for their heights.</returns>
    private static bool SameRow(LayoutLine row, in LayoutItem item) =>
        Math.Abs(item.Bounds.CenterY - row.Bounds.CenterY) <= LineTolerance * Math.Min(item.Bounds.Height, row.Bounds.Height);

    /// <summary>Sorts a line left to right and splits it where a gap is wide enough to be between columns.</summary>
    /// <param name="row">The line.</param>
    /// <param name="output">Receives the pieces.</param>
    private static void SplitColumns(LayoutLine row, List<LayoutLine> output)
    {
        row.Items.Sort(static (a, b) => a.Bounds.Left.CompareTo(b.Bounds.Left));
        var limit = ColumnGapInLine * row.Bounds.Height;
        var piece = new LayoutLine();
        foreach (var item in row.Items)
        {
            if (piece.Items.Count > 0 && item.Bounds.Left - piece.Bounds.Right > limit)
            {
                output.Add(piece);
                piece = new();
            }

            piece.Add(item);
        }

        output.Add(piece);
    }

    /// <summary>Determines whether a line starts a new paragraph.</summary>
    /// <param name="previous">The line above.</param>
    /// <param name="line">The line.</param>
    /// <returns><see langword="true"/> at a wide gap or a change of size.</returns>
    private static bool Breaks(LayoutLine previous, LayoutLine line)
    {
        var a = previous.Bounds.Height;
        var b = line.Bounds.Height;
        return (line.Bounds.Top - previous.Bounds.Bottom) > ParagraphGap * Math.Max(a, b) || Math.Max(a, b) > SizeChange * Math.Min(a, b);
    }

    /// <summary>Groups the items into lines and splits each line at its column gaps.</summary>
    /// <returns>The line pieces.</returns>
    private List<LayoutLine> BuildLines()
    {
        var sorted = new List<LayoutItem>(_items);
        sorted.Sort(static (a, b) => a.Bounds.CenterY.CompareTo(b.Bounds.CenterY));
        var rows = new List<LayoutLine>();
        LayoutLine? row = null;
        foreach (var item in sorted)
        {
            if (row is null || !SameRow(row, item))
            {
                row = new();
                rows.Add(row);
            }

            row.Add(item);
        }

        var pieces = new List<LayoutLine>(rows.Count);
        foreach (var line in rows)
        {
            SplitColumns(line, pieces);
        }

        return pieces;
    }

    /// <summary>Cuts a region at its widest empty band until no band is wide enough, collecting the regions in reading order.</summary>
    /// <param name="region">The lines of the region.</param>
    /// <param name="depth">How deep the cut is.</param>
    /// <param name="output">Receives the regions that are not cut further.</param>
    private void Cut(List<LayoutLine> region, int depth, List<List<LayoutLine>> output)
    {
        if (region.Count <= 1 || depth >= MaxCutDepth)
        {
            output.Add(region);
            return;
        }

        var columns = FindGap(region, true);
        var bands = FindGap(region, false);
        var vertical = columns.Width >= MinColumnGap * _bodyHeight && columns.Width >= bands.Width;
        if (!vertical && bands.Width < MinBandGap * _bodyHeight)
        {
            output.Add(region);
            return;
        }

        var gap = vertical ? columns : bands;
        var first = new List<LayoutLine>();
        var second = new List<LayoutLine>();
        foreach (var line in region)
        {
            var centre = vertical ? line.Bounds.CenterX : line.Bounds.CenterY;
            (centre < gap.Position ? first : second).Add(line);
        }

        Cut(first, depth + 1, output);
        Cut(second, depth + 1, output);
    }

    /// <summary>Groups a region's lines into paragraphs.</summary>
    /// <param name="region">The lines.</param>
    /// <param name="output">Receives the paragraph and heading nodes.</param>
    private void AddParagraphs(List<LayoutLine> region, List<PdfSemanticNode> output)
    {
        if (region.Count == 0)
        {
            return;
        }

        region.Sort(CompareTopDown);
        var paragraph = new List<LayoutLine>();
        LayoutLine? previous = null;
        foreach (var line in region)
        {
            if (previous is not null && Breaks(previous, line))
            {
                output.Add(MakeNode(paragraph));
                paragraph = [];
            }

            paragraph.Add(line);
            previous = line;
        }

        output.Add(MakeNode(paragraph));
    }

    /// <summary>Makes a paragraph or heading node from lines.</summary>
    /// <param name="lines">The lines, top to bottom.</param>
    /// <returns>The node.</returns>
    private PdfSemanticNode MakeNode(List<LayoutLine> lines)
    {
        var items = new List<int>();
        var bounds = default(PdfViewerRect);
        var height = 0F;
        _ = _builder.Clear();
        foreach (var line in lines)
        {
            AppendLine(line, items);
            bounds = bounds.Union(line.Bounds);
            height += line.Bounds.Height;
        }

        var ratio = height / lines.Count / _bodyHeight;
        var heading = lines.Count <= MaxHeadingLines && ratio >= HeadingRatio;
        var level = ratio >= TopHeadingRatio ? 1 : SecondLevel;
        var role = heading ? PdfSemanticRole.Heading : PdfSemanticRole.Paragraph;
        var node = new PdfSemanticNode(role, _origin, _pageIndex) { Level = heading ? level : 0, Text = _builder.ToString(), Bounds = bounds };

        node.SetItems([.. items]);
        return node;
    }

    /// <summary>Appends a line's text and items, with spaces at word gaps and between lines.</summary>
    /// <param name="line">The line.</param>
    /// <param name="items">Receives the item indices.</param>
    private void AppendLine(LayoutLine line, List<int> items)
    {
        var limit = WordGap * line.Bounds.Height;
        var previousRight = float.NaN;
        foreach (var item in line.Items)
        {
            var text = _text.AsSpan(item.TextStart, item.TextLength);
            var separated = float.IsNaN(previousRight) || item.Bounds.Left - previousRight > limit;
            if (separated && NeedsSpace(text))
            {
                _ = _builder.Append(' ');
            }

            _ = _builder.Append(text);
            items.Add(item.Index);
            previousRight = item.Bounds.Right;
        }
    }

    /// <summary>Determines whether a space must go before text so it does not run into what came before.</summary>
    /// <param name="text">The next text.</param>
    /// <returns><see langword="true"/> when neither side has white space.</returns>
    private bool NeedsSpace(ReadOnlySpan<char> text) =>
        _builder.Length > 0 && !char.IsWhiteSpace(_builder[^1]) && !text.IsEmpty && !char.IsWhiteSpace(text[0]);
}
