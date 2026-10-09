// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using PdfViewerLite.App.Services;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Licences;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests the third-party notices embedded in the app, and the Licences window that shows them.</summary>
public sealed class LicencesTests
{
    /// <summary>The fewest components an app with this many dependencies lists; fewer means the notices were not generated.</summary>
    private const int MinComponents = 40;

    /// <summary>The solution file that marks the repository root.</summary>
    private const string SolutionFile = "PdfViewerLite.slnx";

    /// <summary>The restore output of the app project, relative to the repository root.</summary>
    private const string AssetsPath = "src/PdfViewerLite.App/obj/project.assets.json";

    /// <summary>Every shipped package has an entry with a licence text and a licence on the allow list.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryEntryHasTextAndAnAllowedLicence()
    {
        var entries = LicenceNotices.Load().AllEntries();
        var problems = new List<string>();
        foreach (var entry in entries)
        {
            if (entry.Text.Length == 0)
            {
                problems.Add($"{entry.Name} has no licence text.");
            }

            if (!LicenceAllowList.IsAllowed(entry.Licence))
            {
                problems.Add($"{entry.Name} uses {entry.Licence}, which is not on the allow list.");
            }
        }

        await Assert.That(entries.Count).IsGreaterThan(MinComponents);
        await Assert.That(string.Join(Environment.NewLine, problems)).IsEqualTo(string.Empty);
    }

    /// <summary>Every package the app restores for shipping, transitive ones included, appears in the notices.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoticesListEveryShippedPackage()
    {
        var assets = FindAssets();
        if (assets is null)
        {
            Skip.Test("The app's project.assets.json was not found.");
        }

        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in LicenceNotices.Load().AllEntries())
        {
            _ = listed.Add($"{entry.Name}/{entry.Version}");
        }

        var missing = ShippedPackages(assets!).Where(package => !listed.Contains(package)).ToList();

        await Assert.That(string.Join(", ", missing)).IsEqualTo(string.Empty);
    }

    /// <summary>
    /// Native binaries bundled inside a package (the Tesseract runtime packages carry Leptonica, libpng, libtiff, curl,
    /// OpenSSL and others in a <c>licenses</c> folder) are exempt from the package-level allow list, but every one of
    /// their licence texts must appear in that package's entry, so the Licences window shows them.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BundledNativeLicencesAreShown()
    {
        var assets = FindAssets();
        if (assets is null)
        {
            Skip.Test("The app's project.assets.json was not found.");
        }

        var entries = LicenceNotices.Load().AllEntries().ToDictionary(static e => $"{e.Name}/{e.Version}", StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        var bundles = 0;
        foreach (var (package, folder) in PackageFolders(assets!))
        {
            var licences = Path.Combine(folder, "licenses");
            if (!Directory.Exists(licences) || !entries.TryGetValue(package, out var entry))
            {
                continue;
            }

            bundles++;
            foreach (var file in Directory.EnumerateFiles(licences, "*.txt"))
            {
                var firstLine = File.ReadLines(file).Select(static line => line.Trim()).FirstOrDefault(static line => line.Length > 0) ?? string.Empty;
                if (!entry.Text.Contains(firstLine, StringComparison.Ordinal))
                {
                    missing.Add($"{package}: {Path.GetFileName(file)}");
                }
            }
        }

        await Assert.That(bundles).IsGreaterThan(0);
        await Assert.That(string.Join(", ", missing)).IsEqualTo(string.Empty);
    }

    /// <summary>The notices start with this application's own MIT licence.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OwnLicenceComesFirst()
    {
        var first = LicenceNotices.Load().Groups[0];

        await Assert.That(first.Licence).IsEqualTo("MIT");
        await Assert.That(first.Entries[0].Name).IsEqualTo("PdfViewerLite");
        await Assert.That(first.Entries[0].Text).Contains("Permission is hereby granted");
    }

    /// <summary>The bundled fonts and the downloaded models are listed with their sources.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsBundledAndDownloadedComponents()
    {
        var entries = LicenceNotices.Load().AllEntries();
        var fonts = entries.Single(static e => e.Name.StartsWith("Open font pack", StringComparison.Ordinal));
        var voice = entries.Single(static e => e.Name.StartsWith("Kokoro", StringComparison.Ordinal));

        await Assert.That(fonts.Licence).IsEqualTo("Apache-2.0 AND OFL-1.1");
        await Assert.That(fonts.Origin).Contains("on demand");
        await Assert.That(voice.Origin).Contains("Downloaded when you turn this on");
    }

    /// <summary>The window opens, lists the components, shows the chosen text, narrows by search and copies the text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WindowListsComponentsAndShowsText()
    {
        using var licences = new LicencesViewModel(LicenceNotices.Load());
        var window = new LicencesWindow { ViewModel = licences };
        window.Show();
        try
        {
            var tree = window.FindControl<TreeView>("LicenceTree")!;
            var text = window.FindControl<TextBox>("LicenceTextBox")!;
            var search = window.FindControl<TextBox>("SearchBox")!;
            await Assert.That(await UiWait.UntilAsync(() => window.IsLoaded && tree.ItemCount > 1 && text.Text!.Contains("Permission is hereby granted", StringComparison.Ordinal))).IsTrue();
            await Assert.That(licences.DetailTitle).IsEqualTo("PdfViewerLite");

            search.Text = "avalonia";
            await Assert.That(await UiWait.UntilAsync(() => licences.DetailTitle.StartsWith("Avalonia", StringComparison.Ordinal))).IsTrue();
            await Assert.That(licences.Nodes.All(static group => group.Children.All(static node => node.Entry!.Name.Contains("Avalonia", StringComparison.OrdinalIgnoreCase)))).IsTrue();

            _ = await licences.CopyCommand.Execute().ToTask();
            await Assert.That(await window.Clipboard!.TryGetTextAsync()).IsEqualTo(licences.SelectedNode!.Entry!.Text);
            await Assert.That(licences.Status).IsEqualTo("Copied the licence text.");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The About and Licences command asks the window to show the notices.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MenuCommandShowsTheLicences()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        LicencesViewModel? shown = null;
        using var handler = main.ShowLicencesInteraction.RegisterHandler(context =>
        {
            shown = context.Input;
            context.SetOutput(RxVoid.Default);
        });

        _ = await main.LicencesCommand.Execute().ToTask();

        await Assert.That(shown).IsNotNull();
        await Assert.That(shown!.Nodes.Count).IsGreaterThan(1);
    }

    /// <summary>Finds the app's project.assets.json by walking up from the test folder to the repository root.</summary>
    /// <returns>The path, or <see langword="null"/>.</returns>
    private static string? FindAssets()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, SolutionFile)))
        {
            folder = folder.Parent;
        }

        var assets = folder is null ? string.Empty : Path.Combine(folder.FullName, AssetsPath);
        return File.Exists(assets) ? assets : null;
    }

    /// <summary>Finds each shipped package's folder in the package cache.</summary>
    /// <param name="assets">The project.assets.json path.</param>
    /// <returns>The packages as "Id/Version" with their folders, for those present on disk.</returns>
    private static List<PackageFolder> PackageFolders(string assets)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(assets));
        var roots = document.RootElement.GetProperty("packageFolders").EnumerateObject().Select(static p => p.Name).ToList();
        var result = new List<PackageFolder>();
        foreach (var package in ShippedPackages(assets))
        {
            var path = document.RootElement.GetProperty("libraries").GetProperty(package).GetProperty("path").GetString()!;
            if (roots.Select(root => Path.Combine(root, path)).FirstOrDefault(Directory.Exists) is { } folder)
            {
                result.Add(new(package, folder));
            }
        }

        return result;
    }

    /// <summary>Lists the packages the app ships: those with runtime, native or resource files, as "Id/Version".</summary>
    /// <param name="assets">The project.assets.json path.</param>
    /// <returns>The packages.</returns>
    private static List<string> ShippedPackages(string assets)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(assets));
        var shipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in document.RootElement.GetProperty("targets").EnumerateObject())
        {
            foreach (var entry in target.Value.EnumerateObject())
            {
                if (entry.Value.TryGetProperty("runtime", out _) || entry.Value.TryGetProperty("native", out _)
                    || entry.Value.TryGetProperty("runtimeTargets", out _) || entry.Value.TryGetProperty("resource", out _))
                {
                    _ = shipped.Add(entry.Name);
                }
            }
        }

        var result = new List<string>();
        foreach (var library in document.RootElement.GetProperty("libraries").EnumerateObject())
        {
            if (library.Value.GetProperty("type").GetString() == "package" && shipped.Contains(library.Name))
            {
                result.Add(library.Name);
            }
        }

        return result;
    }

    /// <summary>A shipped package and its folder in the package cache.</summary>
    /// <param name="Package">The package as "Id/Version".</param>
    /// <param name="Folder">The folder.</param>
    private readonly record struct PackageFolder(string Package, string Folder);
}
