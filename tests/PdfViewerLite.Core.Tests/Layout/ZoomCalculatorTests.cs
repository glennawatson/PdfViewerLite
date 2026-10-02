// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Layout;

namespace PdfViewerLite.Core.Tests.Layout;

/// <summary>Tests for <see cref="ZoomCalculator"/>.</summary>
public sealed class ZoomCalculatorTests
{
    /// <summary>The comparison tolerance.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>The number of sides a margin applies to.</summary>
    private const double Sides = 2;

    /// <summary>A US letter page.</summary>
    private static readonly PageSize Letter = new(612, 792);

    /// <summary>Verifies fit width fills the available width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FitWidthFillsViewport()
    {
        const double viewportWidth = 1036;
        const double margin = 12;
        var zoom = ZoomCalculator.GetFitZoom([Letter], new(PageRotation.None, PageLayoutMode.Single, ZoomMode.FitWidth, viewportWidth, viewportWidth, 0, margin));

        var expected = (viewportWidth - (Sides * margin)) / Letter.Width / ZoomCalculator.PixelsPerPoint;
        await Assert.That(zoom).IsEqualTo(expected).Within(Tolerance);
    }

    /// <summary>Verifies fit page is limited by height on a wide viewport.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FitPageIsLimitedByHeight()
    {
        const double viewportWidth = 2000;
        const double viewportHeight = 600;
        var zoom = ZoomCalculator.GetFitZoom([Letter], new(PageRotation.None, PageLayoutMode.Single, ZoomMode.FitPage, viewportWidth, viewportHeight, 0, 0));

        await Assert.That(zoom).IsEqualTo(viewportHeight / Letter.Height / ZoomCalculator.PixelsPerPoint).Within(Tolerance);
    }

    /// <summary>Verifies zoom steps move monotonically and clamp.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZoomStepsAreMonotonic()
    {
        const double start = 1.0;

        await Assert.That(ZoomCalculator.ZoomIn(start)).IsGreaterThan(start);
        await Assert.That(ZoomCalculator.ZoomOut(start)).IsLessThan(start);
        await Assert.That(ZoomCalculator.ZoomIn(ZoomCalculator.MaxZoom)).IsEqualTo(ZoomCalculator.MaxZoom);
        await Assert.That(ZoomCalculator.ZoomOut(ZoomCalculator.MinZoom)).IsEqualTo(ZoomCalculator.MinZoom);
    }
}
