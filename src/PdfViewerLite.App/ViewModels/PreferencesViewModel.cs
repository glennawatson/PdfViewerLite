// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Settings;
using ReactiveUI;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// The comfort settings (docs/COMFORT.md, rule 12). Each choice applies at once and is saved; the list indexes match the
/// explicit values of the settings enums.
/// </summary>
[DebuggerDisplay("Preferences")]
public sealed class PreferencesViewModel : ReactiveObject
{
    /// <summary>The label of choices that follow the desktop setting.</summary>
    private const string FollowDesktop = "Follow the desktop";

    /// <summary>The interface text sizes offered, in points; index 0 follows the desktop.</summary>
    private static readonly double[] FontSizes = [0, 9, 10, 11, 12, 14, 16];

    /// <summary>The engines in the order they are offered: MeloTTS first, as the default.</summary>
    private static readonly SpeechEngineChoice[] SpeechEngineChoices = [SpeechEngineChoice.OnDevice, SpeechEngineChoice.Kokoro, SpeechEngineChoice.Azure];

    /// <summary>The services.</summary>
    private readonly AppServices _services;

    /// <summary>Initializes a new instance of the <see cref="PreferencesViewModel"/> class.</summary>
    /// <param name="services">The application services.</param>
    public PreferencesViewModel(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    /// <summary>Gets the colour scheme choices.</summary>
    public static IReadOnlyList<string> ColorSchemeOptions { get; } = [FollowDesktop, "Calm", "High contrast", "Dark", "Light"];

    /// <summary>Gets the page colour choices.</summary>
    public static IReadOnlyList<string> PageToneOptions { get; } = ["Match the colour scheme", "White", "Soft paper", "Calm night", "Dark"];

    /// <summary>Gets the tool bar choices.</summary>
    public static IReadOnlyList<string> ToolbarOptions { get; } = ["Text beside icons", "Icons only"];

    /// <summary>Gets the file change choices.</summary>
    public static IReadOnlyList<string> FileChangeOptions { get; } = ["Reload automatically", "Show a Reload bar"];

    /// <summary>Gets the motion choices.</summary>
    public static IReadOnlyList<string> MotionOptions { get; } = [FollowDesktop, "Reduced", "Normal"];

    /// <summary>Gets the text cursor choices.</summary>
    public static IReadOnlyList<string> CaretOptions { get; } = [FollowDesktop, "Steady", "Blinking"];

    /// <summary>Gets the interface text size choices.</summary>
    public static IReadOnlyList<string> FontSizeOptions { get; } = [FollowDesktop, "9 pt", "10 pt", "11 pt", "12 pt", "14 pt", "16 pt"];

    /// <summary>Gets the Read Aloud voice choices, in the order of <see cref="SpeechEngineChoices"/>.</summary>
    public static IReadOnlyList<string> SpeechEngineOptions { get; } =
    [
        "On this computer: MeloTTS, natural and private (Australian, British, American, Indian)",
        "On this computer: Kokoro (American, British)",
        "Azure AI Speech with your own key",
    ];

    /// <summary>Gets or sets the Read Aloud engine index.</summary>
    public int SpeechEngine
    {
        get => Math.Max(0, Array.IndexOf(SpeechEngineChoices, _services.Settings.SpeechEngine));
        set => Update(
            () => _services.Settings.SpeechEngine = SpeechEngineChoices[Math.Clamp(value, 0, SpeechEngineChoices.Length - 1)],
            nameof(SpeechEngine),
            nameof(UsesAzure));
    }

    /// <summary>Gets a value indicating whether Azure AI Speech is chosen, which shows its key and region.</summary>
    public bool UsesAzure => _services.Settings.SpeechEngine == SpeechEngineChoice.Azure;

    /// <summary>Gets or sets the Azure Speech key.</summary>
    public string AzureKey
    {
        get => _services.Settings.AzureSpeechKey;
        set => Update(() => _services.Settings.AzureSpeechKey = value?.Trim() ?? string.Empty, nameof(AzureKey));
    }

    /// <summary>Gets or sets the timestamp server used when signing with a certificate; empty for none.</summary>
    public string TimestampServer
    {
        get => _services.Settings.TimestampServer;
        set => Update(() => _services.Settings.TimestampServer = value?.Trim() ?? string.Empty, nameof(TimestampServer));
    }

    /// <summary>Gets or sets the Azure Speech region.</summary>
    public string AzureRegion
    {
        get => _services.Settings.AzureSpeechRegion;
        set => Update(() => _services.Settings.AzureSpeechRegion = value?.Trim().ToLowerInvariant() ?? string.Empty, nameof(AzureRegion));
    }

    /// <summary>Gets or sets the colour scheme index.</summary>
    public int ColorScheme
    {
        get => (int)_services.Settings.ColorScheme;
        set => Update(() => _services.Settings.ColorScheme = (ColorSchemeChoice)value, nameof(ColorScheme));
    }

    /// <summary>Gets or sets the page colour index.</summary>
    public int PageTone
    {
        get => (int)_services.Settings.PageTone;
        set => Update(() => _services.Settings.PageTone = (PageToneChoice)value, nameof(PageTone));
    }

    /// <summary>Gets or sets the tool bar style index.</summary>
    public int Toolbar
    {
        get => (int)_services.Settings.ToolbarStyle;
        set => Update(() => _services.Settings.ToolbarStyle = (ToolbarStyle)value, nameof(Toolbar));
    }

    /// <summary>Gets or sets the file change action index.</summary>
    public int FileChange
    {
        get => (int)_services.Settings.FileChangeAction;
        set => Update(() => _services.Settings.FileChangeAction = (FileChangeAction)value, nameof(FileChange));
    }

    /// <summary>Gets or sets the motion index.</summary>
    public int Motion
    {
        get => (int)_services.Settings.Motion;
        set => Update(() => _services.Settings.Motion = (MotionPreference)value, nameof(Motion));
    }

    /// <summary>Gets or sets the text cursor index.</summary>
    public int Caret
    {
        get => (int)_services.Settings.Caret;
        set => Update(() => _services.Settings.Caret = (CaretPreference)value, nameof(Caret));
    }

    /// <summary>Gets or sets the interface text size index.</summary>
    public int FontSize
    {
        get => _services.Settings.InterfaceFontSizePoints is { } points ? Math.Max(0, Array.IndexOf(FontSizes, points)) : 0;
        set => Update(() => _services.Settings.InterfaceFontSizePoints = value > 0 && value < FontSizes.Length ? FontSizes[value] : null, nameof(FontSize));
    }

    /// <summary>Applies a change that affects more than one property, re-themes and saves.</summary>
    /// <param name="change">The change.</param>
    /// <param name="propertyName">The property that changed.</param>
    /// <param name="otherPropertyName">Another property that follows from it.</param>
    private void Update(Action change, string propertyName, string otherPropertyName)
    {
        Update(change, propertyName);
        this.RaisePropertyChanged(otherPropertyName);
    }

    /// <summary>Applies a change, re-themes and saves.</summary>
    /// <param name="change">The change.</param>
    /// <param name="propertyName">The property that changed.</param>
    private void Update(Action change, string propertyName)
    {
        change();
        this.RaisePropertyChanged(propertyName);
        _services.ApplySettings();
    }
}
