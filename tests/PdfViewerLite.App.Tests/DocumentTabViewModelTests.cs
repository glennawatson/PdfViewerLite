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
        await Assert.That(tab.SidebarMode).IsEqualTo(SidebarMode.Search);
        _ = await tab.Search.NextCommand.Execute().ToTask();
        await Assert.That(tab.Search.CurrentIndex).IsEqualTo(1);
        _ = await tab.Search.PreviousCommand.Execute().ToTask();
        _ = await tab.Search.PreviousCommand.Execute().ToTask();
        await Assert.That(tab.Search.CurrentIndex).IsEqualTo(Pages - 1);

        tab.Search.Close();
        await Assert.That(tab.Search.Results.Count).IsEqualTo(0);
    }
}
