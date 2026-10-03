// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Platform;

namespace PdfViewerLite.Core.Tests.Platform;

/// <summary>Tests for <see cref="JsonRecentDocumentStore"/>, the recent list behind the Windows and macOS integrations.</summary>
public sealed class JsonRecentDocumentStoreTests
{
    /// <summary>The list file's name.</summary>
    private const string ListFile = "recent.json";

    /// <summary>The documents created.</summary>
    private const int Documents = 3;

    /// <summary>Documents are listed newest first, reopening moves one to the top, and missing files are skipped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsNewestFirst()
    {
        var folder = Directory.CreateTempSubdirectory("recent-").FullName;
        try
        {
            var store = new JsonRecentDocumentStore(Path.Combine(folder, ListFile), TimeProvider.System);
            var files = new string[Documents];
            for (var i = 0; i < files.Length; i++)
            {
                files[i] = Path.Combine(folder, $"{i}.pdf");
                await File.WriteAllTextAsync(files[i], "%PDF-");
                store.Add(files[i]);
            }

            store.Add(files[0]);
            File.Delete(files[1]);
            var reopened = new JsonRecentDocumentStore(Path.Combine(folder, ListFile), TimeProvider.System).GetRecent(Documents);

            await Assert.That(reopened.Select(static d => d.FileName)).IsEquivalentTo(["0.pdf", "2.pdf"]);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>A missing or damaged list reads as empty.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ToleratesDamage()
    {
        var folder = Directory.CreateTempSubdirectory("recent-").FullName;
        var file = Path.Combine(folder, ListFile);
        try
        {
            await File.WriteAllTextAsync(file, "{ not json");

            await Assert.That(new JsonRecentDocumentStore(file, TimeProvider.System).GetRecent(Documents).Count).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
