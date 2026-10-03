// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.Theming;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Core.Theming;

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

    /// <summary>Masks off the alpha channel.</summary>
    private const uint RgbMask = 0xFFFFFFU;

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

            // The code-behind bindings fill the tool bar.
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var pageCount = view.GetVisualDescendants().OfType<TextBlock>().Single(static t => t.Name == "PageCountText");
            var zoom = view.GetVisualDescendants().OfType<Button>().Single(static b => b.Name == "ZoomButton");
            await Assert.That(pageCount.Text).IsEqualTo($"of {Pages}");
            await Assert.That(zoom.Content as string).EndsWith("%");
            await Assert.That(pageCount.IsVisible && pageCount.Bounds.Width > 0).IsTrue();

            var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
            var scroller = canvas.FindAncestorOfType<ScrollViewer>()!;
            tab.GoToPage(MiddlePage);
            var scrolled = await UiWait.UntilAsync(() => tab.CurrentPageIndex >= MiddlePage - 1);
            await Assert.That(scrolled).IsTrue();
            await Assert.That(scroller.Offset.Y).IsGreaterThan(0);

            test.Services.Settings.ColorScheme = ColorSchemeChoice.Calm;
            test.Services.ApplySettings();
            tab.Search.Open();
            tab.Search.Query = "quick brown";
            _ = await UiWait.UntilAsync(() => tab.Search.Results.Count > 0 && test.Services.RenderHub.Scheduler.QueueLength == 0);
            using var calm = window.CaptureRenderedFrame();
            Save(calm, "calm-search.png");
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

    /// <summary>Verifies each built-in colour scheme themes the chrome and the pages, saving a screenshot of each.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppliesEachScheme()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument("schemes.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        var application = Application.Current!;
        try
        {
            ColorSchemeChoice[] choices = [ColorSchemeChoice.Calm, ColorSchemeChoice.HighContrast, ColorSchemeChoice.Dark, ColorSchemeChoice.Light];
            foreach (var choice in choices)
            {
                test.Services.Settings.ColorScheme = choice;
                test.Services.ApplySettings();
                DesktopThemeApplier.Apply(application, test.Services.CurrentTheme);
                _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Cache.Count > ExpectedTiles && test.Services.RenderHub.Scheduler.QueueLength == 0);
                using var frame = window.CaptureRenderedFrame();
                Save(frame, $"scheme-{choice}.png");

                var foreground = (ISolidColorBrush)application.Resources["AppForeground"]!;
                await Assert.That(foreground.Color.ToUInt32() & RgbMask).IsEqualTo(test.Services.CurrentTheme.Scheme.Text);
                await Assert.That(main.SelectedTab!.PageTone).IsEqualTo(test.Services.CurrentTheme.PageTone);
            }
        }
        finally
        {
            window.Close();
            DesktopThemeApplier.Apply(application, ThemeResolver.Resolve(new(), null));
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
