// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Layout;

namespace PdfViewerLite.Core.Tests.Layout;

/// <summary>Tests for <see cref="DocumentLayout"/>.</summary>
public sealed class DocumentLayoutTests
{
    /// <summary>The page width used in tests.</summary>
    private const float PageWidth = 100F;

    /// <summary>The page height used in tests.</summary>
    private const float PageHeight = 200F;

    /// <summary>The spacing.</summary>
    private const double Spacing = 10;

    /// <summary>The margin.</summary>
    private const double Margin = 20;

    /// <summary>The viewport width.</summary>
    private const double Viewport = 500;

    /// <summary>The number of pages in the test document.</summary>
    private const int Pages = 5;

    /// <summary>The number of rows five pages occupy in either dual mode.</summary>
    private const int DualRows = 3;

    /// <summary>Divisor for centring and halving.</summary>
    private const double Half = 2;

    /// <summary>Verifies single column positions, centring and extent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SingleColumnStacksPages()
    {
        var layout = Create(PageLayoutMode.Single, 1);

        var first = layout.GetPageBounds(0);
        var second = layout.GetPageBounds(1);
        await Assert.That(first).IsEqualTo(new((Viewport - PageWidth) / Half, Margin, PageWidth, PageHeight));
        await Assert.That(second.Y).IsEqualTo(Margin + PageHeight + Spacing);
        await Assert.That(layout.ExtentHeight).IsEqualTo((Half * Margin) + (Pages * PageHeight) + ((Pages - 1) * Spacing));
        await Assert.That(layout.ExtentWidth).IsEqualTo(Viewport);
    }

    /// <summary>Verifies the visible page range lookup.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsVisiblePages()
    {
        var layout = Create(PageLayoutMode.Single, 1);
        const double top = 250;
        const double bottom = 480;
        const int expectedFirst = 1;
        const int expectedLast = 2;

        layout.GetVisiblePages(top, bottom, out var first, out var last);

        await Assert.That(first).IsEqualTo(expectedFirst);
        await Assert.That(last).IsEqualTo(expectedLast);
    }

    /// <summary>Verifies nothing is visible past the end.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NothingVisibleBeyondExtent()
    {
        var layout = Create(PageLayoutMode.Single, 1);

        layout.GetVisiblePages(layout.ExtentHeight + 1, layout.ExtentHeight + Viewport, out var first, out var last);

        await Assert.That(first).IsEqualTo(-1);
        await Assert.That(last).IsLessThan(first);
    }

    /// <summary>Verifies the dual cover arrangement places page 1 alone on the right.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DualCoverPutsFirstPageOnTheRight()
    {
        var layout = Create(PageLayoutMode.DualCover, 1);
        const int secondPage = 1;
        const int thirdPage = 2;

        var cover = layout.GetPageBounds(0);
        var left = layout.GetPageBounds(secondPage);
        var right = layout.GetPageBounds(thirdPage);
        await Assert.That(cover.X).IsGreaterThanOrEqualTo(Viewport / Half);
        await Assert.That(left.Y).IsEqualTo(right.Y);
        await Assert.That(right.X).IsEqualTo(left.Right + Spacing);
        await Assert.That(cover.X).IsEqualTo(right.X);
        await Assert.That(layout.RowCount).IsEqualTo(DualRows);
    }

    /// <summary>Verifies dual mode pairs pages starting from the first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DualPairsFromFirstPage()
    {
        var layout = Create(PageLayoutMode.Dual, 1);

        await Assert.That(layout.GetPageBounds(0).Y).IsEqualTo(layout.GetPageBounds(1).Y);
        await Assert.That(layout.RowCount).IsEqualTo(DualRows);
    }

    /// <summary>Verifies hit testing and nearest page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HitTestsPages()
    {
        var layout = Create(PageLayoutMode.Single, 1);
        var bounds = layout.GetPageBounds(1);
        const int secondPage = 1;

        await Assert.That(layout.HitTest(bounds.X + 1, bounds.Y + 1)).IsEqualTo(secondPage);
        await Assert.That(layout.HitTest(1, bounds.Y + 1)).IsEqualTo(-1);
        await Assert.That(layout.GetPageNearest(bounds.Bottom + (Spacing / Half))).IsEqualTo(secondPage + 1);
    }

    /// <summary>Verifies rotation swaps page dimensions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RotationSwapsDimensions()
    {
        var sizes = Enumerable.Repeat(new PageSize(PageWidth, PageHeight), Pages).ToArray();
        var layout = DocumentLayout.Create(sizes, new(PageRotation.Rotate90, PageLayoutMode.Single, 1, Spacing, Margin, Viewport));

        await Assert.That(layout.GetPageBounds(0).Width).IsEqualTo(PageHeight);
        await Assert.That(layout.GetPageBounds(0).Height).IsEqualTo(PageWidth);
    }

    /// <summary>Creates a layout of identical pages.</summary>
    /// <param name="mode">The mode.</param>
    /// <param name="scale">The scale.</param>
    /// <returns>The layout.</returns>
    private static DocumentLayout Create(PageLayoutMode mode, double scale)
    {
        var sizes = Enumerable.Repeat(new PageSize(PageWidth, PageHeight), Pages).ToArray();
        return DocumentLayout.Create(sizes, new(PageRotation.None, mode, scale, Spacing, Margin, Viewport));
    }
}
