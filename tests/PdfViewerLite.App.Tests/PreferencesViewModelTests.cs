// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.App.Services;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Core.Theming;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests applying and saving PDF page colour choices.</summary>
public sealed class PreferencesViewModelTests
{
    /// <summary>A recognizable persisted fillable-field tint.</summary>
    private const uint LegacyTintColor = 0x123456U;

    /// <summary>Legacy engine settings are ignored and omitted when settings are saved again.</summary>
    /// <param name="legacyChoice">The value saved by an earlier version.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task LegacyPdfEngineSettingIsIgnored(int legacyChoice)
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "settings.json");
        var contents = string.Concat("{\"pdfEngine\":", legacyChoice, ",\"formHighlightColor\":", LegacyTintColor, "}");
        await File.WriteAllTextAsync(path, contents);
        var store = new SettingsStore(path);
        var settings = store.Load();
        store.Save(settings);
        var saved = await File.ReadAllTextAsync(path);

        await Assert.That(settings.FormHighlightColor).IsEqualTo(LegacyTintColor);
        await Assert.That(saved).DoesNotContain("pdfEngine");
    }

    /// <summary>The default app composition opens documents with HyperPDF.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultAppServicesUseHyperPdf()
    {
        using var services = AppServices.CreateDefault(new FallbackPlatform());

        await Assert.That(services.Engine.Name).IsEqualTo("HyperPDF");
    }

    /// <summary>Verifies that choosing a page colour applies even after comfort page colour was turned off.</summary>
    /// <param name="choice">The page colour chosen in Preferences.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PageToneChoice.White)]
    [Arguments(PageToneChoice.Dark)]
    [Arguments(PageToneChoice.FollowDesktop)]
    public async Task ChoosingPageColourAppliesAndSaves(PageToneChoice choice)
    {
        using var test = new TestServices();
        test.Services.Settings.PageToneEnabled = false;
        test.Services.ApplySettings();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument("page-colour.pdf", 1)]);
        var enabled = new List<bool>();
        using var changes = main.WhenAnyValue(static vm => vm.PageToneEnabled).SubscribeSafe(enabled.Add, static error => Trace.TraceError(error.ToString()));
        var preferences = new PreferencesViewModel(test.Services) { PageTone = (int)choice };

        await Assert.That(enabled[^1]).IsTrue();
        await Assert.That(main.SelectedTab!.PageTone).IsEqualTo(ThemeResolver.GetTone(choice, ColorSchemes.Calm));
        await Assert.That(preferences.PageTone).IsEqualTo((int)choice);
        await Assert.That(PreferencesViewModel.PageToneOptions[preferences.PageTone]).IsNotNull();
        var saved = new SettingsStore(Path.Combine(test.Directory, "settings.json")).Load();
        await Assert.That(saved.PageTone).IsEqualTo(choice);
        await Assert.That(saved.PageToneEnabled).IsTrue();
    }
}
