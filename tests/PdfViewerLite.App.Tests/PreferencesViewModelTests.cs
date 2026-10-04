// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Core.Theming;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests applying and saving PDF page colour choices.</summary>
public sealed class PreferencesViewModelTests
{
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
