// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests for <see cref="MainViewModel"/>.</summary>
public sealed class MainViewModelTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 4;

    /// <summary>The number of distinct documents most tests open.</summary>
    private const int TwoTabs = 2;

    /// <summary>The first document name.</summary>
    private const string FirstName = "a.pdf";

    /// <summary>The second document name.</summary>
    private const string SecondName = "b.pdf";

    /// <summary>Verifies opening, de-duplication and selection.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensFilesAsTabs()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var first = test.CreateDocument("first.pdf", Pages);
        var second = test.CreateDocument("second.pdf", Pages);

        main.Open([first, second, first]);

        await Assert.That(main.Tabs.Count).IsEqualTo(TwoTabs);
        await Assert.That(main.HasTabs).IsTrue();
        await Assert.That(main.SelectedTab!.FilePath).IsEqualTo(first);
        await Assert.That(main.SelectedTab.IsLoaded).IsTrue();
        await Assert.That(main.SelectedTab.PageCount).IsEqualTo(Pages);
        await Assert.That(main.Tabs[1].IsLoaded).IsFalse();
    }

    /// <summary>Verifies file URIs from file managers are accepted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensFileUris()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var path = test.CreateDocument("uri.pdf", Pages);

        main.Open([new Uri(path).AbsoluteUri]);

        await Assert.That(main.SelectedTab?.FilePath).IsEqualTo(path);
    }

    /// <summary>Verifies closing selects a neighbour and the tab can be reopened.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosesAndReopensTabs()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var first = test.CreateDocument(FirstName, Pages);
        var second = test.CreateDocument(SecondName, Pages);
        main.Open([first]);
        main.Open([second]);

        main.CloseTab(main.SelectedTab);
        await Assert.That(main.Tabs.Count).IsEqualTo(1);
        await Assert.That(main.SelectedTab!.FilePath).IsEqualTo(first);

        _ = await main.ReopenClosedTabCommand.Execute().ToTask();
        await Assert.That(main.Tabs.Count).IsEqualTo(TwoTabs);
        await Assert.That(main.SelectedTab!.FilePath).IsEqualTo(second);

        _ = await main.CloseAllTabsCommand.Execute().ToTask();
        await Assert.That(main.HasTabs).IsFalse();
    }

    /// <summary>Verifies the session is saved and restored with the selected tab.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RestoresSession()
    {
        using var test = new TestServices();
        var first = test.CreateDocument(FirstName, Pages);
        var second = test.CreateDocument(SecondName, Pages);
        using (var main = new MainViewModel(test.Services))
        {
            main.Open([first, second]);
            main.SelectedTab = main.Tabs[0];
            main.SaveSession();
        }

        using var restored = new MainViewModel(test.Services);
        restored.RestoreSession();

        await Assert.That(restored.Tabs.Count).IsEqualTo(TwoTabs);
        await Assert.That(restored.SelectedTab!.FilePath).IsEqualTo(first);
    }

    /// <summary>Verifies tabs can be reordered.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovesTabs()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument(FirstName, 1), test.CreateDocument(SecondName, 1), test.CreateDocument("c.pdf", 1)]);
        const int last = 2;

        main.MoveTab(0, last);

        await Assert.That(main.Tabs[last].FileName).IsEqualTo(FirstName);
    }

    /// <summary>Verifies a broken file reports an error instead of throwing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BrokenFileShowsError()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var path = Path.Combine(test.Directory, "broken.pdf");
        await File.WriteAllTextAsync(path, "not a pdf");

        main.Open([path]);

        await Assert.That(main.SelectedTab!.ErrorMessage).IsNotNull();
        await Assert.That(main.SelectedTab.IsLoaded).IsFalse();
    }
}
