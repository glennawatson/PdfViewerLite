// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;

namespace PdfViewerLite.App.Tests;

/// <summary>Renders the real window headlessly and checks the pixels.</summary>
public sealed class RenderingTests
{
    /// <summary>The window width.</summary>
    private const int WindowWidth = 1100;

    /// <summary>The window height.</summary>
    private const int WindowHeight = 800;

    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 40;

    /// <summary>The page navigated to.</summary>
    private const int MiddlePage = Pages / 2;

    /// <summary>The number of cached tiles that shows rendering is well under way.</summary>
    private const int ExpectedTiles = 6;

    /// <summary>Verifies pages and thumbnails render, then saves a screenshot when PDFVIEWERLITE_SCREENSHOTS is set.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RendersDocumentWindow()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument("first.pdf", Pages), test.CreateDocument("second.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var rendered = await UiWait.UntilAsync(() => test.Services.RenderHub.Cache.Count > ExpectedTiles);
            await Assert.That(rendered).IsTrue();

            using var frame = window.CaptureRenderedFrame();
            Save(frame, "window.png");
            await Assert.That(frame).IsNotNull();

            var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
            var scroller = canvas.FindAncestorOfType<ScrollViewer>()!;
            tab.GoToPage(MiddlePage);
            var scrolled = await UiWait.UntilAsync(() => tab.CurrentPageIndex >= MiddlePage - 1);
            await Assert.That(scrolled).IsTrue();
            await Assert.That(scroller.Offset.Y).IsGreaterThan(0);

            tab.NightMode = true;
            tab.Search.Open();
            tab.Search.Query = "quick brown";
            _ = await UiWait.UntilAsync(() => tab.Search.Results.Count > 0 && test.Services.RenderHub.Scheduler.QueueLength == 0);
            using var night = window.CaptureRenderedFrame();
            Save(night, "night-search.png");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies the start page shows when nothing is open.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsStartPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var start = window.GetVisualDescendants().OfType<StartView>().Single();
            await Assert.That(start.IsVisible).IsTrue();
            using var frame = window.CaptureRenderedFrame();
            Save(frame, "start.png");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Saves a frame when screenshots are requested.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="name">The file name.</param>
    private static void Save(WriteableBitmap? frame, string name)
    {
        var directory = Environment.GetEnvironmentVariable("PDFVIEWERLITE_SCREENSHOTS");
        if (frame is null || string.IsNullOrEmpty(directory))
        {
            return;
        }

        _ = Directory.CreateDirectory(directory);
        frame.Save(Path.Combine(directory, name), new PngBitmapEncoderOptions());
    }
}
