// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Platform;
using PdfViewerLite.Core.Rendering;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Core.Tabs;
using PdfViewerLite.Core.Theming;
using PdfViewerLite.Http.Remote;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.ViewModels;

/// <summary>The main window: the open tabs, the start page and window level commands.</summary>
[DebuggerDisplay("{Tabs.Count} tabs")]
public sealed class MainViewModel : ReactiveObject, IDisposable
{
    /// <summary>The number of recent documents shown on the start page.</summary>
    private const int RecentCount = 24;

    /// <summary>Closing this many tabs at once asks first.</summary>
    private const int ConfirmCloseThreshold = 2;

    /// <summary>The number of closed tabs that can be reopened.</summary>
    private const int ClosedTabMemory = 32;

    /// <summary>The services.</summary>
    private readonly AppServices _services;

    /// <summary>Recently closed tabs, most recent last; tabs closed together form one group and reopen together.</summary>
    private readonly List<SessionTab[]> _closedTabs = [];

    /// <summary>Emits when the tab finder should open.</summary>
    private readonly Signal<RxVoid> _tabFinderRequests = new();

    /// <summary>Follows the resolved theme so every tab draws pages in the current tone.</summary>
    private readonly IDisposable _themeSubscription;

    /// <summary>Initializes a new instance of the <see cref="MainViewModel"/> class.</summary>
    /// <param name="services">The application services.</param>
    public MainViewModel(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        OpenCommand = ReactiveCommand.CreateFromTask(OpenWithDialogAsync);
        CloseTabCommand = ReactiveCommand.CreateFromTask<DocumentTabViewModel?>(tab => CloseTabAsync(tab ?? SelectedTab));
        CloseOtherTabsCommand = ReactiveCommand.CreateFromTask<DocumentTabViewModel?>(tab => CloseOtherTabsAsync(tab ?? SelectedTab));
        CloseAllTabsCommand = ReactiveCommand.CreateFromTask(CloseAllTabsAsync);
        DismissStatusCommand = ReactiveCommand.Create(() => StatusMessage = null);
        GoToTabCommand = ReactiveCommand.Create<DocumentTabViewModel?>(tab => SelectedTab = tab ?? SelectedTab);
        ShowTabFinderCommand = ReactiveCommand.Create(() => _tabFinderRequests.OnNext(RxVoid.Default));
        NextTabCommand = ReactiveCommand.Create(() => CycleTab(1));
        PreviousTabCommand = ReactiveCommand.Create(() => CycleTab(-1));
        ReopenClosedTabCommand = ReactiveCommand.Create(ReopenClosedTab);
        TogglePageToneCommand = ReactiveCommand.Create(() => PageToneEnabled = !PageToneEnabled);
        OpenRecentCommand = ReactiveCommand.Create<RecentDocument>(recent => Open([recent.FilePath]));
        ShowInFolderCommand = ReactiveCommand.CreateFromTask(ShowInFolderAsync);
        PropertiesCommand = ReactiveCommand.CreateFromTask(ShowPropertiesAsync);
        PreferencesCommand = ReactiveCommand.CreateFromTask(async () => await ShowPreferencesInteraction.Handle(new(services)).ToTask().ConfigureAwait(true));
        RefreshRecentDocuments();
        _themeSubscription = services.Theme.SubscribeSafe(OnTheme, static error => Trace.TraceError(error.ToString()));
    }

    /// <summary>Gets the interaction asking the view for files to open.</summary>
    public Interaction<RxVoid, IReadOnlyList<string>> OpenFileInteraction { get; } = new();

    /// <summary>Gets the interaction asking the user to confirm a destructive action.</summary>
    public Interaction<ConfirmRequest, bool> ConfirmInteraction { get; } = new();

    /// <summary>Gets the interaction asking the view to show the preferences.</summary>
    public Interaction<PreferencesViewModel, RxVoid> ShowPreferencesInteraction { get; } = new();

    /// <summary>Gets the interaction asking the view to show document properties.</summary>
    public Interaction<DocumentTabViewModel, RxVoid> ShowPropertiesInteraction { get; } = new();

    /// <summary>Gets the open tabs.</summary>
    public ObservableCollection<DocumentTabViewModel> Tabs { get; } = [];

    /// <summary>Gets the tabs matching <see cref="TabQuery"/>, shown in the tab finder.</summary>
    public ObservableCollection<DocumentTabViewModel> FoundTabs { get; } = [];

    /// <summary>Gets or sets what the user typed in the tab finder.</summary>
    public string TabQuery
    {
        get;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref field, value);
            RefreshFoundTabs();
        }
    } = string.Empty;

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

    /// <summary>Gets a value indicating whether any tab has unsaved annotations or form entries.</summary>
    public bool HasUnsavedTabs
    {
        get
        {
            foreach (var tab in Tabs)
            {
                if (tab.HasUnsavedChanges)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Gets the window title.</summary>
    public string WindowTitle => SelectedTab is { } tab ? $"{tab.Title} — PdfViewerLite" : "PdfViewerLite";

    /// <summary>Gets or sets a value indicating whether pages are drawn in the comfort page colour rather than plain white.</summary>
    public bool PageToneEnabled
    {
        get => _services.Settings.PageToneEnabled;
        set
        {
            if (value == _services.Settings.PageToneEnabled)
            {
                return;
            }

            _services.Settings.PageToneEnabled = value;
            this.RaisePropertyChanged();
            _services.ApplySettings();
        }
    }

    /// <summary>Gets the page tone tabs draw with.</summary>
    public PageTone PageTone
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = PageTone.None;

    /// <summary>Gets a status message, for example a failed download; it stays until dismissed.</summary>
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

    /// <summary>Gets the command selecting a tab from the tab finder.</summary>
    public ReactiveCommand<DocumentTabViewModel?, RxVoid> GoToTabCommand { get; }

    /// <summary>Gets the command opening the tab finder.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ShowTabFinderCommand { get; }

    /// <summary>Gets the requests to open the tab finder.</summary>
    public IObservable<RxVoid> TabFinderRequests => _tabFinderRequests;

    /// <summary>Gets the command dismissing the status message.</summary>
    public ReactiveCommand<RxVoid, string?> DismissStatusCommand { get; }

    /// <summary>Gets the next tab command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> NextTabCommand { get; }

    /// <summary>Gets the previous tab command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> PreviousTabCommand { get; }

    /// <summary>Gets the reopen closed tab command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ReopenClosedTabCommand { get; }

    /// <summary>Gets the command switching between the comfort page colour and plain white pages.</summary>
    public ReactiveCommand<RxVoid, bool> TogglePageToneCommand { get; }

    /// <summary>Gets the command opening a recent document.</summary>
    public ReactiveCommand<RecentDocument, RxVoid> OpenRecentCommand { get; }

    /// <summary>Gets the command showing the selected document in the file manager.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ShowInFolderCommand { get; }

    /// <summary>Gets the preferences command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> PreferencesCommand { get; }

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

    /// <summary>Closes a tab straight away, even with unsaved edits; the tab can be reopened.</summary>
    /// <param name="tab">The tab.</param>
    public void CloseTabWithoutAsking(DocumentTabViewModel? tab)
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

        Remember([tab]);
        Close(tab, index);
    }

    /// <summary>Refreshes the tab finder's list from <see cref="TabQuery"/>.</summary>
    public void RefreshFoundTabs()
    {
        var summaries = new TabSummary[Tabs.Count];
        for (var i = 0; i < summaries.Length; i++)
        {
            var tab = Tabs[i];
            summaries[i] = new(tab.FileName, tab.Title, tab.Folder);
        }

        var matches = new int[summaries.Length];
        var count = TabFilter.Filter(TabQuery, summaries, matches);
        FoundTabs.Clear();
        for (var i = 0; i < count; i++)
        {
            FoundTabs.Add(Tabs[matches[i]]);
        }
    }

    /// <summary>Asks whether to discard unsaved edits in every tab; true when there are none.</summary>
    /// <returns><see langword="true"/> to go ahead.</returns>
    public async Task<bool> ConfirmDiscardAsync()
    {
        var unsaved = 0;
        foreach (var tab in Tabs)
        {
            unsaved += tab.HasUnsavedChanges ? 1 : 0;
        }

        if (unsaved == 0)
        {
            return true;
        }

        var request = new ConfirmRequest(
            unsaved == 1 ? "Close without saving?" : $"Close {unsaved} documents without saving?",
            "Annotations and form entries you have not saved will be lost. Choose Cancel, then Save (Ctrl+S) to keep them.",
            "Close Without Saving");
        return await ConfirmInteraction.Handle(request).ToTask().ConfigureAwait(true);
    }

    /// <summary>Closes a tab, asking first when it has unsaved edits.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns>A task.</returns>
    public async Task CloseTabAsync(DocumentTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        if (tab.HasUnsavedChanges)
        {
            var request = new ConfirmRequest(
                $"Close {tab.FileName} without saving?",
                "Annotations and form entries you have not saved will be lost. Choose Cancel, then Save (Ctrl+S) to keep them.",
                "Close Without Saving");
            if (!await ConfirmInteraction.Handle(request).ToTask().ConfigureAwait(true))
            {
                return;
            }
        }

        CloseTabWithoutAsking(tab);
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
        _themeSubscription.Dispose();
        _tabFinderRequests.Dispose();
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

        var tab = new DocumentTabViewModel(_services.Pool.Create(path, null), _services) { PageTone = PageTone };
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

    /// <summary>Closes every tab except one, asking first when more than one would close.</summary>
    /// <param name="keep">The tab to keep.</param>
    /// <returns>A task.</returns>
    private async Task CloseOtherTabsAsync(DocumentTabViewModel? keep)
    {
        var closing = new List<DocumentTabViewModel>(Tabs.Count);
        foreach (var tab in Tabs)
        {
            if (tab != keep)
            {
                closing.Add(tab);
            }
        }

        if (await ConfirmCloseAsync(closing.Count).ConfigureAwait(true))
        {
            CloseGroup(closing);
        }
    }

    /// <summary>Closes every tab, asking first when there is more than one.</summary>
    /// <returns>A task.</returns>
    private async Task CloseAllTabsAsync()
    {
        if (!await ConfirmCloseAsync(Tabs.Count).ConfigureAwait(true))
        {
            return;
        }

        CloseGroup([.. Tabs]);
        RefreshRecentDocuments();
    }

    /// <summary>Asks before closing several tabs at once.</summary>
    /// <param name="count">The number of tabs that would close.</param>
    /// <returns><see langword="true"/> to go ahead.</returns>
    private async Task<bool> ConfirmCloseAsync(int count)
    {
        if (count < ConfirmCloseThreshold)
        {
            return count == 0 || await ConfirmDiscardAsync().ConfigureAwait(true);
        }

        var request = new ConfirmRequest($"Close {count} tabs?", "You can reopen them together with Reopen Closed Tabs (Ctrl+Shift+T).", "Close Tabs");
        return await ConfirmInteraction.Handle(request).ToTask().ConfigureAwait(true);
    }

    /// <summary>Closes several tabs as one group that reopens together.</summary>
    /// <param name="closing">The tabs.</param>
    private void CloseGroup(List<DocumentTabViewModel> closing)
    {
        if (closing.Count == 0)
        {
            return;
        }

        Remember(closing);
        foreach (var tab in closing)
        {
            Close(tab, Tabs.IndexOf(tab));
        }
    }

    /// <summary>Removes and disposes a tab, selecting a neighbour when it was selected.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="index">Its index.</param>
    private void Close(DocumentTabViewModel tab, int index)
    {
        if (index < 0)
        {
            return;
        }

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

    /// <summary>Reopens the most recently closed tab, or every tab of a group closed together.</summary>
    private void ReopenClosedTab()
    {
        if (_closedTabs.Count == 0)
        {
            return;
        }

        var group = _closedTabs[^1];
        _closedTabs.RemoveAt(_closedTabs.Count - 1);
        DocumentTabViewModel? last = null;
        foreach (var entry in group)
        {
            if (File.Exists(entry.FilePath))
            {
                last = OpenOrFind(entry.FilePath, entry.PageIndex);
            }
        }

        if (last is not null)
        {
            SelectedTab = last;
        }
    }

    /// <summary>Remembers closed tabs so they can be reopened together.</summary>
    /// <param name="tabs">The tabs.</param>
    private void Remember(List<DocumentTabViewModel> tabs)
    {
        if (_closedTabs.Count >= ClosedTabMemory)
        {
            _closedTabs.RemoveAt(0);
        }

        var group = new SessionTab[tabs.Count];
        for (var i = 0; i < group.Length; i++)
        {
            group[i] = new() { FilePath = tabs[i].FilePath, PageIndex = Math.Max(0, tabs[i].CurrentPageIndex) };
        }

        _closedTabs.Add(group);
    }

    /// <summary>Updates <see cref="HasTabs"/>.</summary>
    private void UpdateHasTabs() => HasTabs = Tabs.Count > 0;

    /// <summary>Applies a new theme's page tone to every tab.</summary>
    /// <param name="theme">The theme.</param>
    private void OnTheme(ResolvedTheme theme)
    {
        PageTone = theme.PageTone;
        foreach (var tab in Tabs)
        {
            tab.PageTone = theme.PageTone;
        }
    }
}
