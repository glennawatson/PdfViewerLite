// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Platform;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Http.Remote;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.ViewModels;

/// <summary>The main window: the open tabs, the start page and window level commands.</summary>
[DebuggerDisplay("{Tabs.Count} tabs")]
public sealed class MainViewModel : ReactiveObject, IDisposable
{
    /// <summary>The number of recent documents shown on the start page.</summary>
    private const int RecentCount = 24;

    /// <summary>The number of closed tabs that can be reopened.</summary>
    private const int ClosedTabMemory = 32;

    /// <summary>The services.</summary>
    private readonly AppServices _services;

    /// <summary>Recently closed tabs, most recent last.</summary>
    private readonly List<SessionTab> _closedTabs = [];

    /// <summary>Initializes a new instance of the <see cref="MainViewModel"/> class.</summary>
    /// <param name="services">The application services.</param>
    public MainViewModel(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        NightMode = services.Settings.NightMode;
        OpenCommand = ReactiveCommand.CreateFromTask(OpenWithDialogAsync);
        CloseTabCommand = ReactiveCommand.Create<DocumentTabViewModel?>(tab => CloseTab(tab ?? SelectedTab));
        CloseOtherTabsCommand = ReactiveCommand.Create<DocumentTabViewModel?>(tab => CloseOtherTabs(tab ?? SelectedTab));
        CloseAllTabsCommand = ReactiveCommand.Create(CloseAllTabs);
        NextTabCommand = ReactiveCommand.Create(() => CycleTab(1));
        PreviousTabCommand = ReactiveCommand.Create(() => CycleTab(-1));
        ReopenClosedTabCommand = ReactiveCommand.Create(ReopenClosedTab);
        ToggleNightModeCommand = ReactiveCommand.Create(() => NightMode = !NightMode);
        OpenRecentCommand = ReactiveCommand.Create<RecentDocument>(recent => Open([recent.FilePath]));
        ShowInFolderCommand = ReactiveCommand.CreateFromTask(ShowInFolderAsync);
        PropertiesCommand = ReactiveCommand.CreateFromTask(ShowPropertiesAsync);
        RefreshRecentDocuments();
    }

    /// <summary>Gets the interaction asking the view for files to open.</summary>
    public Interaction<RxVoid, IReadOnlyList<string>> OpenFileInteraction { get; } = new();

    /// <summary>Gets the interaction asking the view to show document properties.</summary>
    public Interaction<DocumentTabViewModel, RxVoid> ShowPropertiesInteraction { get; } = new();

    /// <summary>Gets the open tabs.</summary>
    public ObservableCollection<DocumentTabViewModel> Tabs { get; } = [];

    /// <summary>Gets the recently opened documents shown on the start page.</summary>
    public ObservableCollection<RecentDocument> RecentDocuments { get; } = [];

    /// <summary>Gets or sets the selected tab.</summary>
    public DocumentTabViewModel? SelectedTab
    {
        get;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref field, value);
            value?.EnsureLoaded();
            this.RaisePropertyChanged(nameof(WindowTitle));
        }
    }

    /// <summary>Gets a value indicating whether any tab is open.</summary>
    public bool HasTabs
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the window title.</summary>
    public string WindowTitle => SelectedTab is { } tab ? $"{tab.Title} — PdfViewerLite" : "PdfViewerLite";

    /// <summary>Gets or sets a value indicating whether pages are drawn with inverted colours.</summary>
    public bool NightMode
    {
        get;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref field, value);
            _services.Settings.NightMode = value;
            foreach (var tab in Tabs)
            {
                tab.NightMode = value;
            }
        }
    }

    /// <summary>Gets a transient status message, for example a failed download.</summary>
    public string? StatusMessage
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the open command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> OpenCommand { get; }

    /// <summary>Gets the close tab command; a null parameter closes the selected tab.</summary>
    public ReactiveCommand<DocumentTabViewModel?, RxVoid> CloseTabCommand { get; }

    /// <summary>Gets the close other tabs command.</summary>
    public ReactiveCommand<DocumentTabViewModel?, RxVoid> CloseOtherTabsCommand { get; }

    /// <summary>Gets the close all tabs command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> CloseAllTabsCommand { get; }

    /// <summary>Gets the next tab command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> NextTabCommand { get; }

    /// <summary>Gets the previous tab command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> PreviousTabCommand { get; }

    /// <summary>Gets the reopen closed tab command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ReopenClosedTabCommand { get; }

    /// <summary>Gets the night mode toggle command.</summary>
    public ReactiveCommand<RxVoid, bool> ToggleNightModeCommand { get; }

    /// <summary>Gets the command opening a recent document.</summary>
    public ReactiveCommand<RecentDocument, RxVoid> OpenRecentCommand { get; }

    /// <summary>Gets the command showing the selected document in the file manager.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ShowInFolderCommand { get; }

    /// <summary>Gets the document properties command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> PropertiesCommand { get; }

    /// <summary>Opens files, file URIs or web addresses, selecting the last one. Already open files are selected instead.</summary>
    /// <param name="items">Paths or URIs.</param>
    public void Open(IEnumerable<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        DocumentTabViewModel? last = null;
        foreach (var item in items)
        {
            if (RemoteDocumentDownloader.IsRemote(item, out var remote))
            {
                _ = OpenRemoteAsync(remote);
                continue;
            }

            var path = ToLocalPath(item);
            if (path is not null)
            {
                last = OpenOrFind(path, 0);
            }
        }

        if (last is not null)
        {
            SelectedTab = last;
        }
    }

    /// <summary>Restores the tabs open at the end of the previous run.</summary>
    public void RestoreSession()
    {
        if (!_services.Settings.RestoreSession)
        {
            return;
        }

        DocumentTabViewModel? selected = null;
        foreach (var entry in _services.Settings.Session)
        {
            if (!File.Exists(entry.FilePath))
            {
                continue;
            }

            var tab = OpenOrFind(entry.FilePath, entry.PageIndex);
            if (entry.IsSelected)
            {
                selected = tab;
            }
        }

        SelectedTab = selected ?? (Tabs.Count > 0 ? Tabs[0] : null);
    }

    /// <summary>Records the open tabs and saves settings.</summary>
    public void SaveSession()
    {
        var session = _services.Settings.Session;
        session.Clear();
        foreach (var tab in Tabs)
        {
            session.Add(new() { FilePath = tab.FilePath, PageIndex = Math.Max(0, tab.CurrentPageIndex), IsSelected = tab == SelectedTab });
        }

        if (SelectedTab is { } current)
        {
            _services.Settings.ShowSidebar = current.SidebarVisible;
        }

        _services.SaveSettings();
    }

    /// <summary>Records the window size and state for the next start.</summary>
    /// <param name="width">The window width.</param>
    /// <param name="height">The window height.</param>
    /// <param name="maximized">Whether the window is maximised.</param>
    /// <param name="restored">Whether the window is in its normal state, so its size is meaningful.</param>
    public void RememberWindow(double width, double height, bool maximized, bool restored)
    {
        var settings = _services.Settings;
        settings.WindowMaximized = maximized;
        if (!restored)
        {
            return;
        }

        settings.WindowWidth = width;
        settings.WindowHeight = height;
    }

    /// <summary>Moves a tab, used by drag and drop reordering.</summary>
    /// <param name="oldIndex">The current index.</param>
    /// <param name="newIndex">The new index.</param>
    public void MoveTab(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex || (uint)oldIndex >= (uint)Tabs.Count || (uint)newIndex >= (uint)Tabs.Count)
        {
            return;
        }

        var selected = SelectedTab;
        Tabs.Move(oldIndex, newIndex);
        SelectedTab = selected;
    }

    /// <summary>Closes a tab.</summary>
    /// <param name="tab">The tab.</param>
    public void CloseTab(DocumentTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        var index = Tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        Remember(tab);
        var wasSelected = tab == SelectedTab;
        Tabs.RemoveAt(index);
        tab.Dispose();
        _services.Pool.Remove(tab.Source);
        if (wasSelected)
        {
            SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Min(index, Tabs.Count - 1)];
        }

        UpdateHasTabs();
    }

    /// <summary>Reloads the recent documents list.</summary>
    public void RefreshRecentDocuments()
    {
        RecentDocuments.Clear();
        foreach (var recent in _services.RecentDocuments.GetRecent(RecentCount))
        {
            RecentDocuments.Add(recent);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var tab in Tabs)
        {
            tab.Dispose();
        }

        Tabs.Clear();
    }

    /// <summary>Converts a path or <c>file://</c> URI to a full local path.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The path, or <see langword="null"/> when it is not local.</returns>
    private static string? ToLocalPath(string item)
    {
        if (string.IsNullOrWhiteSpace(item))
        {
            return null;
        }

        if (Uri.TryCreate(item, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return uri.LocalPath;
        }

        return Path.IsPathRooted(item) || !item.Contains("://", StringComparison.Ordinal) ? Path.GetFullPath(item) : null;
    }

    /// <summary>Finds an open tab for a file or opens a new one.</summary>
    /// <param name="path">The full path.</param>
    /// <param name="pageIndex">The page to show in a new tab.</param>
    /// <returns>The tab.</returns>
    private DocumentTabViewModel OpenOrFind(string path, int pageIndex)
    {
        foreach (var existing in Tabs)
        {
            if (string.Equals(existing.FilePath, path, StringComparison.Ordinal))
            {
                return existing;
            }
        }

        var tab = new DocumentTabViewModel(_services.Pool.Create(path, null), _services) { NightMode = NightMode };
        if (pageIndex > 0)
        {
            tab.ReportPosition(new(pageIndex, 0), pageIndex);
        }

        var insertAt = SelectedTab is { } selected ? Tabs.IndexOf(selected) + 1 : Tabs.Count;
        Tabs.Insert(Math.Clamp(insertAt, 0, Tabs.Count), tab);
        UpdateHasTabs();
        return tab;
    }

    /// <summary>Downloads a remote document and opens it.</summary>
    /// <param name="uri">The URI.</param>
    /// <returns>A task.</returns>
    private async Task OpenRemoteAsync(Uri uri)
    {
        StatusMessage = $"Downloading {uri.Host}…";
        try
        {
            var path = await _services.Downloader.DownloadAsync(uri, CancellationToken.None).ConfigureAwait(true);
            StatusMessage = null;
            Open([path]);
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = $"Download failed: {ex.Message}";
        }
        catch (InvalidDataException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    /// <summary>Asks the view for files and opens them.</summary>
    /// <returns>A task.</returns>
    private async Task OpenWithDialogAsync()
    {
        var files = await OpenFileInteraction.Handle(RxVoid.Default);
        Open(files);
    }

    /// <summary>Asks the view to show properties of the selected document.</summary>
    /// <returns>A task.</returns>
    private async Task ShowPropertiesAsync()
    {
        if (SelectedTab is { IsLoaded: true } tab)
        {
            _ = await ShowPropertiesInteraction.Handle(tab);
        }
    }

    /// <summary>Shows the selected document in the file manager.</summary>
    /// <returns>A task.</returns>
    private async Task ShowInFolderAsync()
    {
        if (SelectedTab is { } tab && !await _services.FileManager.ShowItemAsync(tab.FilePath, CancellationToken.None).ConfigureAwait(true))
        {
            StatusMessage = "No file manager is available.";
        }
    }

    /// <summary>Closes every tab except one.</summary>
    /// <param name="keep">The tab to keep.</param>
    private void CloseOtherTabs(DocumentTabViewModel? keep)
    {
        for (var i = Tabs.Count - 1; i >= 0; i--)
        {
            if (Tabs[i] != keep)
            {
                CloseTab(Tabs[i]);
            }
        }
    }

    /// <summary>Closes every tab.</summary>
    private void CloseAllTabs()
    {
        for (var i = Tabs.Count - 1; i >= 0; i--)
        {
            CloseTab(Tabs[i]);
        }

        RefreshRecentDocuments();
    }

    /// <summary>Selects the next or previous tab, wrapping around.</summary>
    /// <param name="direction">1 or -1.</param>
    private void CycleTab(int direction)
    {
        if (Tabs.Count == 0)
        {
            return;
        }

        var index = SelectedTab is null ? 0 : Tabs.IndexOf(SelectedTab);
        SelectedTab = Tabs[(index + direction + Tabs.Count) % Tabs.Count];
    }

    /// <summary>Reopens the most recently closed tab.</summary>
    private void ReopenClosedTab()
    {
        while (_closedTabs.Count > 0)
        {
            var entry = _closedTabs[^1];
            _closedTabs.RemoveAt(_closedTabs.Count - 1);
            if (!File.Exists(entry.FilePath))
            {
                continue;
            }

            SelectedTab = OpenOrFind(entry.FilePath, entry.PageIndex);
            return;
        }
    }

    /// <summary>Remembers a closed tab so it can be reopened.</summary>
    /// <param name="tab">The tab.</param>
    private void Remember(DocumentTabViewModel tab)
    {
        if (_closedTabs.Count >= ClosedTabMemory)
        {
            _closedTabs.RemoveAt(0);
        }

        _closedTabs.Add(new() { FilePath = tab.FilePath, PageIndex = Math.Max(0, tab.CurrentPageIndex) });
    }

    /// <summary>Updates <see cref="HasTabs"/>.</summary>
    private void UpdateHasTabs() => HasTabs = Tabs.Count > 0;
}
