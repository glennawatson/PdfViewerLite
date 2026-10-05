// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using PdfViewerLite.App.Controls;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks that thumbnail navigation leaves visible previews still.</summary>
public sealed class ThumbnailListBoxTests
{
    /// <summary>The test viewport height.</summary>
    private const double Viewport = 300;

    /// <summary>The content height.</summary>
    private const double Extent = 2000;

    /// <summary>The virtualized item count.</summary>
    private const int ItemCount = 80;

    /// <summary>The item height.</summary>
    private const double ItemHeight = 100;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 250;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 360;

    /// <summary>The second visible selection.</summary>
    private const int VisibleIndex = 2;

    /// <summary>The selection beyond the viewport.</summary>
    private const int OutsideIndex = 5;

    /// <summary>The visible selection after paging.</summary>
    private const int PreviousIndex = 4;

    /// <summary>A distant unrealized selection.</summary>
    private const int DistantIndex = 70;

    /// <summary>Checks visible, clipped, distant and end-of-document thumbnails.</summary>
    /// <param name="offset">The starting offset.</param>
    /// <param name="top">The item top.</param>
    /// <param name="bottom">The item bottom.</param>
    /// <param name="expected">The expected offset.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(300, 320, 420, 300)]
    [Arguments(300, 580, 680, 300)]
    [Arguments(300, 240, 340, 300)]
    [Arguments(300, 600, 700, 600)]
    [Arguments(300, 200, 300, 0)]
    [Arguments(0, 1500, 1600, 1500)]
    [Arguments(1500, 0, 100, 0)]
    [Arguments(1500, 1900, 2000, 1700)]
    public async Task PagesOnlyWhenItemLeavesView(double offset, double top, double bottom, double expected) =>
        await Assert.That(ThumbnailViewport.GetOffset(offset, Viewport, top, bottom, Extent)).IsEqualTo(expected);

    /// <summary>Checks that a virtualized list follows selections by a viewport and keeps visible selections still.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsVisibleSelectionsStill()
    {
        var list = new ThumbnailListBox { ItemsSource = Enumerable.Range(0, ItemCount).ToArray(), ItemTemplate = new FuncDataTemplate<int>(static (_, _) => new Border { Height = ItemHeight }) };
        var window = new Window { Content = list, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            await Assert.That(await UiWait.UntilAsync(() => list.Scroll is ScrollViewer { Viewport.Height: > 0 })).IsTrue();
            var scroll = (ScrollViewer)list.Scroll!;
            var initial = scroll.Offset.Y;
            list.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            await Assert.That(scroll.Offset.Y).IsEqualTo(initial);
            list.SelectedIndex = VisibleIndex;
            Dispatcher.UIThread.RunJobs();
            await Assert.That(scroll.Offset.Y).IsEqualTo(initial);
            list.SelectedIndex = OutsideIndex;
            await Assert.That(await UiWait.UntilAsync(() => scroll.Offset.Y > initial)).IsTrue();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            await Assert.That(scroll.Offset.Y).IsEqualTo(scroll.Viewport.Height).Within(1);
            var page = scroll.Offset.Y;
            list.SelectedIndex = PreviousIndex;
            Dispatcher.UIThread.RunJobs();
            await Assert.That(scroll.Offset.Y).IsEqualTo(page);
            list.SelectedIndex = 0;
            await Assert.That(await UiWait.UntilAsync(() => scroll.Offset.Y == 0)).IsTrue();
            list.SelectedIndex = DistantIndex;
            await Assert.That(await UiWait.UntilAsync(() => list.ContainerFromIndex(DistantIndex) is not null)).IsTrue();
            await Assert.That(scroll.Offset.Y).IsGreaterThan(page);
        }
        finally
        {
            window.Close();
        }
    }
}
