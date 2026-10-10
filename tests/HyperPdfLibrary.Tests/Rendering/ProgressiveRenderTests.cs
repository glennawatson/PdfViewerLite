// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks progressive rendering: pausing between slices, resuming, cancelling, and matching a normal render.</summary>
public sealed class ProgressiveRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The rectangles drawn, enough for several slices.</summary>
    private const int Rectangles = 2000;

    /// <summary>The operators in one rectangle: colour, path and fill.</summary>
    private const int OperatorsPerRectangle = 3;

    /// <summary>The colours cycled through.</summary>
    private const int Colours = 3;

    /// <summary>The untouched target marker.</summary>
    private const byte Sentinel = 0xA5;

    /// <summary>A long page pauses between slices and, resumed, draws exactly what a normal render draws.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PausedRenderResumesToTheSameImage()
    {
        var pdf = CreateBusyPage();
        using var progressive = new RenderTestPage(pdf);
        using var normal = new RenderTestPage(pdf);
        var pixels = new byte[Size * Size * RenderedImage.BytesPerPixel];
        pixels.AsSpan().Fill(Sentinel);
        var request = new PdfTileRequest(0, 1, 0, 0, 0, PdfRenderFlags.None);

        var paused = progressive.Renderer.RenderProgressive(request, new(pixels, Size, Size, Size * RenderedImage.BytesPerPixel), static () => true, CancellationToken.None);
        var untouched = pixels.AsSpan().IndexOfAnyExcept(Sentinel) < 0;

        var calls = RenderUntilDone(progressive.Renderer, request, pixels);
        var expected = normal.RenderPage();

        await Assert.That(calls).IsGreaterThan(1);
        await Assert.That(paused == PdfRenderStatus.Paused && untouched).IsTrue();
        await Assert.That(pixels.AsSpan().SequenceEqual(expected.Pixels)).IsTrue();
    }

    /// <summary>A cancelled render stops, discards its recording, and a later render still completes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledRenderStopsAndCanStartAgain()
    {
        using var page = new RenderTestPage(CreateBusyPage());
        var pixels = new byte[Size * Size * RenderedImage.BytesPerPixel];
        pixels.AsSpan().Fill(Sentinel);
        var request = new PdfTileRequest(0, 1, 0, 0, 0, PdfRenderFlags.None);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var cancelled = page.Renderer.RenderProgressive(request, new(pixels, Size, Size, Size * RenderedImage.BytesPerPixel), null, cancellation.Token);
        var untouched = pixels.AsSpan().IndexOfAnyExcept(Sentinel) < 0;
        var finished = page.Renderer.RenderProgressive(request, new(pixels, Size, Size, Size * RenderedImage.BytesPerPixel), null, CancellationToken.None);

        await Assert.That(cancelled).IsEqualTo(PdfRenderStatus.Cancelled);
        await Assert.That(untouched).IsTrue();
        await Assert.That(finished).IsEqualTo(PdfRenderStatus.Done);
    }

    /// <summary>A request for a missing page fails.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingPageFails()
    {
        using var page = new RenderTestPage(CreateBusyPage());
        var pixels = new byte[Size * Size * RenderedImage.BytesPerPixel];

        var status = page.Renderer.RenderProgressive(new(1, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, Size, Size, Size * RenderedImage.BytesPerPixel), null, CancellationToken.None);

        await Assert.That(status).IsEqualTo(PdfRenderStatus.Failed);
    }

    /// <summary>Renders progressively, pausing after every slice, until done.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="request">The tile request.</param>
    /// <param name="pixels">The target pixels.</param>
    /// <returns>The calls it took.</returns>
    private static int RenderUntilDone(PdfPageRenderer renderer, in PdfTileRequest request, byte[] pixels)
    {
        var calls = 0;
        var status = PdfRenderStatus.Paused;
        while (status == PdfRenderStatus.Paused)
        {
            status = renderer.RenderProgressive(request, new(pixels, Size, Size, Size * RenderedImage.BytesPerPixel), static () => true, CancellationToken.None);
            calls++;
        }

        return status == PdfRenderStatus.Done ? calls : -1;
    }

    /// <summary>Creates a page of many small rectangles.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateBusyPage()
    {
        var content = new StringBuilder();
        for (var i = 0; i < Rectangles; i++)
        {
            var x = i % Size;
            var y = i / Size * OperatorsPerRectangle % Size;
            var colour = i % Colours;
            _ = content.Append(CultureInfo.InvariantCulture, $"{(colour == 0 ? 1 : 0)} {(colour == 1 ? 1 : 0)} {(colour == Colours - 1 ? 1 : 0)} rg {x} {y} 2 2 re f ");
        }

        return new RenderTestPdf(Size, Size) { Content = content.ToString() }.ToBytes();
    }
}
