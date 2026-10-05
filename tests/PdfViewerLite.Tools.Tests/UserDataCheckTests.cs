// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Settings;
using PdfViewerLite.Tools.Commands;

namespace PdfViewerLite.Tools.Tests;

/// <summary>Tests for <see cref="UserDataCheck"/>.</summary>
public sealed class UserDataCheckTests
{
    /// <summary>The settings path below a seeded root.</summary>
    private static readonly string[] SettingsPath = [UserDataCheck.ConfigFolder, "pdfviewerlite", "settings.json"];

    /// <summary>The viewer reads the seeded preferences from the folder the check gives it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ViewerReadsSeededPreferences()
    {
        var root = CreateRoot();
        try
        {
            var data = UserDataCheck.Seed(Path.Combine(root, "user"), await CreateDocumentAsync(root));
            var settings = new SettingsStore(Path.Combine(data.ConfigHome, "pdfviewerlite", "settings.json")).Load();

            await Assert.That(settings.SignatureName).IsEqualTo(UserDataCheck.SignatureName);
            await Assert.That(settings.FocusTextWidth).IsEqualTo(UserDataCheck.FocusTextWidth);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>The viewer may save other settings without failing the check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AcceptsViewerSavingOtherSettings()
    {
        var root = CreateRoot();
        try
        {
            var data = UserDataCheck.Seed(Path.Combine(root, "user"), await CreateDocumentAsync(root));
            var store = new SettingsStore(Path.Combine([data.Root, .. SettingsPath]));
            var settings = store.Load();
            settings.WindowMaximized = true;
            store.Save(settings);

            data.Verify("a test launch");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>A lost preference fails the check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsResetPreferences()
    {
        var root = CreateRoot();
        try
        {
            var data = UserDataCheck.Seed(Path.Combine(root, "user"), await CreateDocumentAsync(root));
            new SettingsStore(Path.Combine([data.Root, .. SettingsPath])).Save(new());

            await Assert.That(() => data.Verify("a reset")).Throws<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>A removed settings file or document fails the check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsRemovedData()
    {
        var root = CreateRoot();
        try
        {
            var data = UserDataCheck.Seed(Path.Combine(root, "user"), await CreateDocumentAsync(root));
            File.Delete(Path.Combine(data.Root, "Documents", "package-check-notes.pdf"));
            await Assert.That(() => data.Verify("removing a document")).Throws<InvalidOperationException>();

            Directory.Delete(data.ConfigHome, true);
            await Assert.That(() => data.Verify("removing the settings")).Throws<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>A changed document fails the check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsChangedDocument()
    {
        var root = CreateRoot();
        try
        {
            var data = UserDataCheck.Seed(Path.Combine(root, "user"), await CreateDocumentAsync(root));
            await File.AppendAllTextAsync(Path.Combine(data.Root, "Documents", "package-check-notes.pdf"), "changed");

            await Assert.That(() => data.Verify("changing a document")).Throws<InvalidOperationException>();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>Data copied back from a container is checked against the original seed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VerifiesCopiedData()
    {
        var root = CreateRoot();
        try
        {
            var data = UserDataCheck.Seed(Path.Combine(root, "user"), await CreateDocumentAsync(root));
            var copy = Path.Combine(root, "copy");
            CopyDirectory(data.Root, copy);

            data.VerifyCopy(copy, "copying");
            await Assert.That(Directory.Exists(copy)).IsTrue();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    /// <summary>Creates an empty temporary folder.</summary>
    /// <returns>The folder.</returns>
    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-userdata-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>Writes a small stand-in document.</summary>
    /// <param name="root">The temporary folder.</param>
    /// <returns>The document path.</returns>
    private static async Task<string> CreateDocumentAsync(string root)
    {
        var document = Path.Combine(root, "source.pdf");
        await File.WriteAllTextAsync(document, "%PDF-1.7\n%%EOF\n");
        return document;
    }

    /// <summary>Copies a folder tree.</summary>
    /// <param name="source">The source folder.</param>
    /// <param name="target">The new target folder.</param>
    private static void CopyDirectory(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var copy = Path.Combine(target, Path.GetRelativePath(source, file));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(file, copy);
        }
    }
}
