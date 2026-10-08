// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Layout;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests for <see cref="DocumentTabViewModel"/> and <see cref="SearchViewModel"/>.</summary>
public sealed class DocumentTabViewModelTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 6;

    /// <summary>The generated document name.</summary>
    private const string DocumentName = "doc.pdf";

    /// <summary>The number of navigation requests the test makes.</summary>
    private const int RequestCount = 3;

    /// <summary>Verifies document structure is loaded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LoadsStructure()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument(DocumentName, Pages)]);
        var tab = main.SelectedTab!;

        await Assert.That(tab.Thumbnails.Count).IsEqualTo(Pages);
        await Assert.That(tab.Outline.Count).IsEqualTo(Pages);
        await Assert.That(tab.HasOutline).IsTrue();
        await Assert.That(tab.Title).IsEqualTo("PdfViewerLite Test Document");
        await Assert.That(tab.PageEntry).IsEqualTo("1");
    }

    /// <summary>Verifies navigation requests and page entry parsing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NavigatesToPages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument(DocumentName, Pages)]);
        var tab = main.SelectedTab!;
        var requests = new List<NavigationRequest>();
        using var navigation = tab.NavigationRequests.SubscribeSafe(requests.Add, static _ => { });
        const int fourthPage = 3;

        tab.PageEntry = "4";
        _ = await tab.GoToPageEntryCommand.Execute().ToTask();
        _ = await tab.LastPageCommand.Execute().ToTask();
        tab.Navigate(tab.Outline[1].Target);

        await Assert.That(requests.Count).IsEqualTo(RequestCount);
        await Assert.That(requests[0].PageIndex).IsEqualTo(fourthPage);
        await Assert.That(requests[1].PageIndex).IsEqualTo(Pages - 1);
        await Assert.That(requests[2].PageIndex).IsEqualTo(1);
    }

    /// <summary>Verifies Back and Forward return across jumps, ignore page steps, and are only enabled when they can move.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GoesBackAndForwardAcrossJumps()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument(DocumentName, Pages)]);
        var tab = main.SelectedTab!;
        var requests = new List<NavigationRequest>();
        using var navigation = tab.NavigationRequests.SubscribeSafe(requests.Add, static _ => { });
        var canGoBack = false;
        using var back = tab.GoBackCommand.CanExecute.SubscribeSafe(can => canGoBack = can, static _ => { });
        const int jumpPage = 4;

        var enabledAtStart = canGoBack;
        _ = await tab.NextPageCommand.Execute().ToTask();
        var enabledAfterStep = canGoBack;
        tab.ReportPosition(new(1, 0), 1);
        tab.GoToPage(jumpPage);
        tab.ReportPosition(new(jumpPage, 0), jumpPage);
        var enabledAfterJump = canGoBack;
        _ = await tab.GoBackCommand.Execute().ToTask();

        await Assert.That(enabledAtStart).IsFalse();
        await Assert.That(enabledAfterStep).IsFalse();
        await Assert.That(enabledAfterJump).IsTrue();
        await Assert.That(requests[^1].PageIndex).IsEqualTo(1);
        await Assert.That(tab.CanGoBack).IsFalse();
        await Assert.That(tab.CanGoForward).IsTrue();
    }

    /// <summary>Verifies zoom and rotation commands.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ZoomsAndRotates()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument(DocumentName, Pages)]);
        var tab = main.SelectedTab!;

        _ = await tab.ZoomResetCommand.Execute().ToTask();
        _ = await tab.ZoomInCommand.Execute().ToTask();
        await Assert.That(tab.ZoomMode).IsEqualTo(ZoomMode.Free);
        await Assert.That(tab.Zoom).IsGreaterThan(1);

        _ = await tab.SetZoomCommand.Execute("200").ToTask();
        await Assert.That(tab.ZoomText).IsEqualTo("200%");

        _ = await tab.FitPageCommand.Execute().ToTask();
        await Assert.That(tab.ZoomMode).IsEqualTo(ZoomMode.FitPage);

        _ = await tab.RotateRightCommand.Execute().ToTask();
        await Assert.That(tab.Rotation).IsEqualTo(PageRotation.Rotate90);
        _ = await tab.RotateLeftCommand.Execute().ToTask();
        _ = await tab.RotateLeftCommand.Execute().ToTask();
        await Assert.That(tab.Rotation).IsEqualTo(PageRotation.Rotate270);
    }

    /// <summary>
    /// Verifies a search started again part way through keeps nothing from the run it replaced: the page the old run
    /// was reading when it was cancelled is not added to the new results.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RestartedSearchKeepsOnlyItsOwnResults()
    {
        const int rounds = 100;
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument(DocumentName, Pages)]);
        var tab = main.SelectedTab!;
        tab.Search.Query = "lazy dog";
        _ = await UiWait.UntilAsync(() => !tab.Search.IsSearching && tab.Search.Results.Count == Pages);

        var counts = new List<int>(rounds);
        for (var i = 0; i < rounds; i++)
        {
            // The second start cancels the first while the thread pool is often already reading its first page.
            tab.Search.Refresh();
            tab.Search.Refresh();
            _ = await UiWait.UntilAsync(() => !tab.Search.IsSearching && tab.Search.Results.Count >= Pages);
            counts.Add(tab.Search.Results.Count);
        }

        await Assert.That(counts.FindAll(static count => count != Pages)).IsEmpty();
    }

    /// <summary>Verifies search finds results incrementally and moves between them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SearchesDocument()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument(DocumentName, Pages)]);
        var tab = main.SelectedTab!;

        tab.Search.Open();
        tab.Search.Query = "lazy dog";
        var found = await UiWait.UntilAsync(() => !tab.Search.IsSearching && tab.Search.Results.Count == Pages);

        await Assert.That(found).IsTrue();
        await Assert.That(tab.Search.CurrentIndex).IsEqualTo(0);
        await Assert.That(tab.Search.Status).IsEqualTo($"1 of {Pages}");
        await Assert.That(tab.SidebarMode).IsEqualTo(SidebarMode.Thumbnails);
        _ = await tab.Search.NextCommand.Execute().ToTask();
        await Assert.That(tab.Search.CurrentIndex).IsEqualTo(1);
        _ = await tab.Search.PreviousCommand.Execute().ToTask();
        _ = await tab.Search.PreviousCommand.Execute().ToTask();
        await Assert.That(tab.Search.CurrentIndex).IsEqualTo(Pages - 1);

        tab.Search.Close();
        await Assert.That(tab.Search.Results.Count).IsEqualTo(0);
    }

    /// <summary>Verifies opening and closing find leaves the sidebar on the panel the user chose.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindKeepsSidebarPanel()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument(DocumentName, Pages)]);
        var tab = main.SelectedTab!;
        tab.SidebarMode = SidebarMode.Outline;

        tab.Search.Open();
        var whileOpen = tab.SidebarMode;
        tab.Search.Close();

        await Assert.That(whileOpen).IsEqualTo(SidebarMode.Outline);
        await Assert.That(tab.SidebarMode).IsEqualTo(SidebarMode.Outline);
    }
}
