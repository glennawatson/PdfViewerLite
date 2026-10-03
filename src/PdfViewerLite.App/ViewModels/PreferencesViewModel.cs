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
