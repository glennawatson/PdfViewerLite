// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Layout;
using PdfViewerLite.Core.Settings;

namespace PdfViewerLite.Core.Tests.Settings;

/// <summary>Tests for <see cref="SettingsStore"/>.</summary>
public sealed class SettingsStoreTests
{
    /// <summary>Verifies settings survive a save and load.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RoundTrips()
    {
        const int cacheSize = 512;
        const int page = 3;
        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-{Guid.NewGuid():N}", "settings.json");
        var store = new SettingsStore(path);
        var settings = new AppSettings { NightMode = true, DefaultZoomMode = ZoomMode.FitPage, TileCacheMegabytes = cacheSize };
        settings.Session.Add(new() { FilePath = "/tmp/a.pdf", PageIndex = page, IsSelected = true });

        store.Save(settings);
        var loaded = store.Load();

        await Assert.That(loaded.NightMode).IsTrue();
        await Assert.That(loaded.DefaultZoomMode).IsEqualTo(ZoomMode.FitPage);
        await Assert.That(loaded.TileCacheMegabytes).IsEqualTo(cacheSize);
        await Assert.That(loaded.Session.Count).IsEqualTo(1);
        await Assert.That(loaded.Session[0].FilePath).IsEqualTo("/tmp/a.pdf");
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    /// <summary>Verifies a corrupt file falls back to defaults.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CorruptFileGivesDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{ not json");

        var loaded = new SettingsStore(path).Load();

        await Assert.That(loaded.ShowSidebar).IsTrue();
        File.Delete(path);
    }
}
