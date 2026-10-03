// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.App.Rendering;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Platform;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Core.Theming;
using PdfViewerLite.Http.Remote;
using PdfViewerLite.Pdfium;
using PdfViewerLite.Platform.Linux.DBus;
using PdfViewerLite.Platform.Linux.Kde;
using PdfViewerLite.Platform.Linux.Recent;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.Services;

/// <summary>The composition root: every long lived service, created once at start up.</summary>
[DebuggerDisplay("AppServices")]
public sealed class AppServices : IDisposable
{
    /// <summary>Bytes in a megabyte.</summary>
    private const long BytesPerMegabyte = 1024 * 1024;

    /// <summary>The smallest permitted tile cache, in megabytes.</summary>
    private const int MinCacheMegabytes = 32;

    /// <summary>The resolved theme, re-published when the settings or the desktop palette change.</summary>
    private readonly BehaviorSignal<ResolvedTheme> _theme;

    /// <summary>The latest desktop palette, if any.</summary>
    private DesktopPalette? _palette;

    /// <summary>Initializes a new instance of the <see cref="AppServices"/> class.</summary>
    /// <param name="settingsStore">The settings store.</param>
    /// <param name="engine">The document engine.</param>
    /// <param name="recentDocuments">The recent documents store.</param>
    /// <param name="fileManager">The file manager launcher.</param>
    /// <param name="themeSource">The desktop theme source, if any.</param>
    public AppServices(SettingsStore settingsStore, IDocumentEngine engine, IRecentDocumentStore recentDocuments, IFileManagerLauncher fileManager, IDesktopThemeSource? themeSource)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        SettingsStore = settingsStore;
        Settings = settingsStore.Load();
        Pool = new(engine, Math.Max(1, Settings.MaxOpenDocuments));
        RenderHub = new(Math.Max(MinCacheMegabytes, Settings.TileCacheMegabytes) * BytesPerMegabyte);
        RecentDocuments = recentDocuments;
        FileManager = fileManager;
        ThemeSource = themeSource;
        Downloader = new(Path.Combine(Path.GetTempPath(), "pdfviewerlite-downloads"));
        _theme = new(ThemeResolver.Resolve(Settings, null));
    }

    /// <summary>Gets the settings store.</summary>
    public SettingsStore SettingsStore { get; }

    /// <summary>Gets the user settings.</summary>
    public AppSettings Settings { get; }

    /// <summary>Gets the document pool.</summary>
    public DocumentPool Pool { get; }

    /// <summary>Gets the render hub.</summary>
    public RenderHub RenderHub { get; }

    /// <summary>Gets the recent documents store.</summary>
    public IRecentDocumentStore RecentDocuments { get; }

    /// <summary>Gets the file manager launcher.</summary>
    public IFileManagerLauncher FileManager { get; }

    /// <summary>Gets the desktop theme source, if any.</summary>
    public IDesktopThemeSource? ThemeSource { get; }

    /// <summary>Gets the remote document downloader.</summary>
    public RemoteDocumentDownloader Downloader { get; }

    /// <summary>Gets the resolved theme; the current value is replayed on subscription.</summary>
    public IObservable<ResolvedTheme> Theme => _theme;

    /// <summary>Gets the current resolved theme.</summary>
    public ResolvedTheme CurrentTheme => _theme.Value;

    /// <summary>Creates the services for the current platform.</summary>
    /// <returns>The services.</returns>
    public static AppServices CreateDefault()
    {
        var engine = new PdfiumEngine();
        return OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD()
            ? new(new SettingsStore(), engine, new XbelRecentDocumentStore(), new DBusFileManagerLauncher(), new KdeThemeSource())
            : new(new SettingsStore(), engine, new NullRecentDocumentStore(), new NullFileManagerLauncher(), null);
    }

    /// <summary>Records a new desktop palette and re-resolves the theme. Call on the UI thread.</summary>
    /// <param name="palette">The palette, or <see langword="null"/> when the desktop has none.</param>
    public void SetDesktopPalette(DesktopPalette? palette)
    {
        _palette = palette;
        RefreshTheme();
    }

    /// <summary>Re-resolves the theme after <see cref="Settings"/> changed and saves the settings. Call on the UI thread.</summary>
    public void ApplySettings()
    {
        RefreshTheme();
        SaveSettings();
    }

    /// <summary>Saves the settings, ignoring IO failures.</summary>
    public void SaveSettings()
    {
        try
        {
            SettingsStore.Save(Settings);
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"Could not save settings: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Debug.WriteLine($"Could not save settings: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _theme.Dispose();
        RenderHub.Dispose();
        Pool.Dispose();
        (ThemeSource as IDisposable)?.Dispose();
    }

    /// <summary>Publishes the theme when it changed.</summary>
    private void RefreshTheme()
    {
        var theme = ThemeResolver.Resolve(Settings, _palette);
        if (theme != _theme.Value)
        {
            _theme.OnNext(theme);
        }
    }
}
