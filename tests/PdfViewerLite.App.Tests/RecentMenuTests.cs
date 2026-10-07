// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Platform;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests the Open Recent menu, which stays available while documents are open.</summary>
public sealed class RecentMenuTests
{
    /// <summary>Verifies Open Recent lists recent documents while a document is open, and opens one at its last page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensRecentDocumentFromMenu()
    {
        const int pages = 3;
        const int lastPage = 2;
        const int bothTabs = 2;
        var storePath = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-recent-{Guid.NewGuid():N}.json");
        var store = new JsonRecentDocumentStore(storePath, TimeProvider.System);
        using var test = new TestServices(new PrintingPlatform(new RecordingPrinter(), store));
        var earlier = test.CreateDocument("earlier.pdf", pages);
        test.Services.RecentDocuments.Add(earlier);
        Core.Settings.LastPages.Remember(test.Services.Settings.LastPages, earlier, lastPage);
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument("current.pdf", 1)]);
        var window = new MainWindow { DataContext = main };
        window.Show();
        try
        {
            var menuButton = window.FindControl<Button>("TabsMenuButton")!;
            menuButton.Flyout!.ShowAt(menuButton);
            var recent = window.FindControl<MenuItem>("RecentMenuItem")!;
            await Assert.That(await UiWait.UntilAsync(() => recent.IsEnabled)).IsTrue();
            recent.Open();
            await Assert.That(await UiWait.UntilAsync(() => FindRecentItem(recent, earlier) is { Command: not null })).IsTrue();
            var item = FindRecentItem(recent, earlier)!;
            await Assert.That(ControlHelp.GetText(item)).Contains("earlier.pdf");
            item.Command!.Execute(item.CommandParameter);

            await Assert.That(await UiWait.UntilAsync(() => main.SelectedTab?.FilePath == earlier)).IsTrue();
            await Assert.That(main.SelectedTab!.CurrentPageIndex).IsEqualTo(lastPage);
            await Assert.That(main.Tabs.Count).IsEqualTo(bothTabs);
        }
        finally
        {
            window.Close();
            File.Delete(storePath);
        }
    }

    /// <summary>Finds the menu item for a recent document.</summary>
    /// <param name="recent">The Open Recent menu.</param>
    /// <param name="path">The document.</param>
    /// <returns>The item, or <see langword="null"/> before it is shown.</returns>
    private static MenuItem? FindRecentItem(MenuItem recent, string path)
    {
        foreach (var container in recent.GetRealizedContainers())
        {
            if (container is MenuItem { DataContext: RecentDocument document } item && document.FilePath == path)
            {
                return item;
            }
        }

        return null;
    }
}
