// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.Views;

/// <summary>The main window: a strip of document tabs above the selected document.</summary>
/// <summary>The main window: the tab strip, the start page and the document of the selected tab.</summary>
[DebuggerDisplay("{Title}")]
public sealed partial class MainWindow : Window, IViewFor<MainViewModel>
{
    /// <summary>Defines the <see cref="ViewModel"/> property.</summary>
    public static readonly StyledProperty<MainViewModel?> ViewModelProperty = AvaloniaProperty.Register<MainWindow, MainViewModel?>(nameof(ViewModel));

    /// <summary>How far the pointer must move before a tab drag starts.</summary>
    private const double DragThreshold = 6;

    /// <summary>Pixels the tab strip scrolls per wheel notch.</summary>
    private const double TabWheelStep = 60;

    /// <summary>The PDF file type filter.</summary>
    private static readonly FilePickerFileType PdfFileType = new("PDF documents") { Patterns = ["*.pdf", "*.PDF"], MimeTypes = ["application/pdf"] };

    /// <summary>Input subscriptions for the window's lifetime.</summary>
    private readonly MultipleDisposable _inputSubscriptions;

    /// <summary>Bindings and interaction handlers made while the window is shown.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>The Search in Folder window, while it is open.</summary>
    private FolderSearchWindow? _folderSearch;

    /// <summary>Forgets the Search in Folder window when it closes.</summary>
    private IDisposable? _folderSearchClosed;

    /// <summary>The tab being dragged.</summary>
    private DocumentTabViewModel? _draggedTab;

    /// <summary>Where the tab drag started.</summary>
    private Point _dragStart;

    /// <summary>The window state before presenting started, restored when it ends.</summary>
    private WindowState? _stateBeforePresenting;

    /// <summary>Whether the user agreed to close with unsaved edits.</summary>
    private bool _closeConfirmed;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
        TabStrip.ItemTemplate = new FuncDataTemplate<DocumentTabViewModel>(static (_, _) => new TabItemView());
        TabFinderList.ItemTemplate = new FuncDataTemplate<DocumentTabViewModel>(static (_, _) => new TabSummaryView());
        DocumentHost.ContentTemplate = new FuncDataTemplate<DocumentTabViewModel>(static (_, _) => new DocumentView(), true);
        _inputSubscriptions =
        [
            this.GetObservable(DragDrop.DragOverEvent).SubscribeSafe(OnDragOver, OnError),
            this.GetObservable(DragDrop.DropEvent).SubscribeSafe(OnDrop, OnError),
            this.GetObservable(KeyDownEvent, RoutingStrategies.Tunnel).SubscribeSafe(OnPreviewKeyDown, OnError),
            TabStrip.GetObservable(PointerPressedEvent, RoutingStrategies.Tunnel).SubscribeSafe(OnTabPointerPressed, OnError),
            TabStrip.GetObservable(PointerMovedEvent, RoutingStrategies.Tunnel).SubscribeSafe(OnTabPointerMoved, OnError),
            TabStrip.GetObservable(PointerReleasedEvent, RoutingStrategies.Tunnel).SubscribeSafe(_ => _draggedTab = null, OnError),
            TabStrip.GetObservable(PointerWheelChangedEvent, RoutingStrategies.Tunnel).SubscribeSafe(OnTabWheel, OnError),
            TabStrip.GetObservable(ToolTip.ToolTipOpeningEvent).SubscribeSafe(OnTabPreviewOpening, OnError),
            TabStrip.GetObservable(Button.ClickEvent, RoutingStrategies.Bubble).SubscribeSafe(OnTabButtonClick, OnError),
            TabStrip.GetObservable(ContextRequestedEvent, RoutingStrategies.Bubble).SubscribeSafe(OnTabContextRequested, OnError),
            TabFinderList.GetObservable(SelectingItemsControl.SelectionChangedEvent).SubscribeSafe(_ => OnTabFound(), OnError),
            TabFinderBox.GetObservable(KeyDownEvent, RoutingStrategies.Bubble).Where(static args => args.Key == Key.Enter).SubscribeSafe(_ => PickFirstFoundTab(), OnError),
            Signal.FromEvent<EventHandler, RxVoid>(
                static handler => (_, _) => handler(RxVoid.Default),
                handler => TabFinderButton.Flyout!.Opened += handler,
                handler => TabFinderButton.Flyout!.Opened -= handler).SubscribeSafe(_ => OnTabFinderOpened(), OnError),
        ];
    }

    /// <inheritdoc/>
    public MainViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = value as MainViewModel;
    }

    /// <summary>Brings the window to the front, for example when another launch forwards files.</summary>
    public void BringToFront()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ViewModel = DataContext as MainViewModel;
    }

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _bindings =
        [
            this.OneWayBind(ViewModel, static vm => vm.WindowTitle, static v => v.Title),
            this.OneWayBind(ViewModel, static vm => vm.Tabs, static v => v.TabStrip.ItemsSource),
            this.Bind(ViewModel, static vm => vm.SelectedTab, static v => v.TabStrip.SelectedItem, static tab => tab, static item => item as DocumentTabViewModel),
            this.OneWayBind(ViewModel, static vm => vm.HasTabs, static v => v.TabFinderButton.IsVisible),
            this.OneWayBind(ViewModel, static vm => vm.Tabs.Count, static v => v.TabCountText.Text, static count => string.Create(CultureInfo.CurrentCulture, $"Tabs {count}")),
            this.Bind(ViewModel, static vm => vm.TabQuery, static v => v.TabFinderBox.Text, static query => query, static text => text ?? string.Empty),
            this.OneWayBind(ViewModel, static vm => vm.FoundTabs, static v => v.TabFinderList.ItemsSource),
            this.BindCommand(ViewModel, static vm => vm.OpenCommand, static v => v.OpenButton),
            this.BindCommand(ViewModel, static vm => vm.OpenCommand, static v => v.OpenMenuItem),
            this.BindCommand(ViewModel, static vm => vm.ReopenClosedTabCommand, static v => v.ReopenMenuItem),
            this.BindCommand(ViewModel, static vm => vm.TogglePageToneCommand, static v => v.PageToneMenuItem),
            this.OneWayBind(ViewModel, static vm => vm.PageToneEnabled, static v => v.PageToneMenuItem.IsChecked),
            this.BindCommand(ViewModel, static vm => vm.PreferencesCommand, static v => v.PreferencesMenuItem),
            this.BindCommand(ViewModel, static vm => vm.SearchFolderCommand, static v => v.SearchFolderMenuItem),
            this.BindCommand(ViewModel, static vm => vm.CloseAllTabsCommand, static v => v.CloseAllMenuItem),
            this.OneWayBind(ViewModel, static vm => vm.StatusMessage, static v => v.StatusText.Text),
            this.OneWayBind(ViewModel, static vm => vm.StatusMessage, static v => v.StatusBar.IsVisible, static message => message is not null),
            this.BindCommand(ViewModel, static vm => vm.DismissStatusCommand, static v => v.DismissStatusButton),
            this.OneWayBind(ViewModel, static vm => vm.HasTabs, static v => v.StartPage.IsVisible, static hasTabs => !hasTabs),
            this.OneWayBind(ViewModel, static vm => vm.HasTabs, static v => v.DocumentHost.IsVisible),
            this.OneWayBind(ViewModel, static vm => vm.SelectedTab, static v => v.DocumentHost.Content),
            this.BindInteraction(ViewModel, static vm => vm.OpenFileInteraction, OpenFilesAsync),
            this.BindInteraction(ViewModel, static vm => vm.ShowPropertiesInteraction, ShowPropertiesAsync),
            this.BindInteraction(ViewModel, static vm => vm.ConfirmInteraction, ConfirmAsync),
            this.BindInteraction(ViewModel, static vm => vm.ShowPreferencesInteraction, ShowPreferencesAsync),
            this.BindInteraction(ViewModel, static vm => vm.ShowFolderSearchInteraction, ShowFolderSearchAsync),
            this.WhenAnyObservable(static v => v.ViewModel!.TabFinderRequests).SubscribeSafe(_ => TabFinderButton.Flyout?.ShowAt(TabFinderButton), OnError),
            this.WhenAnyValue(static v => v.ViewModel!.SelectedTab!.IsPresenting).SubscribeSafe(OnPresentingChanged, OnError),
        ];
    }

    /// <inheritdoc/>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnClosing(e);
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (!_closeConfirmed && viewModel.HasUnsavedTabs)
        {
            e.Cancel = true;
            _ = ConfirmCloseAsync(viewModel);
            return;
        }

        viewModel.RememberWindow(Width, Height, WindowState == WindowState.Maximized, WindowState == WindowState.Normal);
        viewModel.SaveSession();
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _inputSubscriptions.Dispose();
        _bindings?.Dispose();
        _bindings = null;
    }

    /// <summary>Converts storage items to local paths.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The paths.</returns>
    private static List<string> ToPaths(IEnumerable<IStorageItem> items)
    {
        var paths = new List<string>();
        foreach (var item in items)
        {
            if (item.TryGetLocalPath() is { } path)
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Readies the hover preview of the tab under the pointer.</summary>
    /// <param name="e">The event.</param>
    private static void OnTabPreviewOpening(CancelRoutedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is DocumentTabViewModel tab)
        {
            tab.PreparePreview();
        }
    }

    /// <summary>Accepts dragged files.</summary>
    /// <param name="e">The event.</param>
    private static void OnDragOver(DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) || e.DataTransfer.Contains(DataFormat.Text) ? DragDropEffects.Copy : DragDropEffects.None;

    /// <summary>Shows the open dialog; on KDE this goes through the XDG portal and shows the KDE file dialog.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task OpenFilesAsync(IInteractionContext<RxVoid, IReadOnlyList<string>> context)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Open Document", AllowMultiple = true, FileTypeFilter = [PdfFileType, FilePickerFileTypes.All] });
        context.SetOutput(ToPaths(files));
    }

    /// <summary>Refreshes the tab finder and focuses its search box.</summary>
    private void OnTabFinderOpened()
    {
        ViewModel?.RefreshFoundTabs();
        TabFinderList.SelectedItem = null;
        _ = TabFinderBox.Focus();
        TabFinderBox.SelectAll();
    }

    /// <summary>Selects the tab picked in the tab finder and closes it.</summary>
    private void OnTabFound()
    {
        if (TabFinderList.SelectedItem is not DocumentTabViewModel tab || ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.SelectedTab = tab;
        TabFinderButton.Flyout?.Hide();
        TabStrip.ScrollIntoView(tab);
    }

    /// <summary>Asks before closing the window with unsaved edits, then closes when the user agrees.</summary>
    /// <param name="viewModel">The view model.</param>
    /// <returns>A task.</returns>
    private async Task ConfirmCloseAsync(MainViewModel viewModel)
    {
        if (!await viewModel.ConfirmDiscardAsync().ConfigureAwait(true))
        {
            return;
        }

        _closeConfirmed = true;
        Close();
    }

    /// <summary>Closes a tab when its close button is clicked.</summary>
    /// <param name="e">The event.</param>
    private void OnTabButtonClick(RoutedEventArgs e)
    {
        if (e.Source is not Button { Name: "CloseButton", DataContext: DocumentTabViewModel tab } || ViewModel is not { } viewModel)
        {
            return;
        }

        _ = viewModel.CloseTabCommand.Execute(tab).Subscribe();
        e.Handled = true;
    }

    /// <summary>Opens the menu of the tab under the pointer: close it, close the others, close all, reload.</summary>
    /// <param name="e">The event.</param>
    private void OnTabContextRequested(ContextRequestedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is not DocumentTabViewModel tab || ViewModel is not { } viewModel || e.Source is not Control source)
        {
            return;
        }

        var menu = new ContextMenu
        {
            ItemsSource = new Control[]
            {
                new MenuItem { Header = "_Close", Command = viewModel.CloseTabCommand, CommandParameter = tab, InputGesture = new(Key.W, KeyModifiers.Control) },
                new MenuItem { Header = "Close _Other Tabs", Command = viewModel.CloseOtherTabsCommand, CommandParameter = tab },
                new MenuItem { Header = "Close _All Tabs", Command = viewModel.CloseAllTabsCommand },
                new Separator(),
                new MenuItem { Header = "_Save", Command = tab.SaveCommand, IsEnabled = tab.HasUnsavedChanges },
                new MenuItem { Header = "Re_load", Command = tab.ReloadCommand },
            },
        };
        menu.Open(source);
        e.Handled = true;
    }

    /// <summary>Selects the first tab the finder lists.</summary>
    private void PickFirstFoundTab()
    {
        if (ViewModel is { FoundTabs.Count: > 0 } viewModel)
        {
            TabFinderList.SelectedItem = viewModel.FoundTabs[0];
        }
    }

    /// <summary>Asks the user to confirm a destructive action.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ConfirmAsync(IInteractionContext<ConfirmRequest, bool> context)
    {
        var dialog = new ConfirmWindow { ViewModel = context.Input };
        context.SetOutput(await dialog.ShowDialog<bool>(this));
    }

    /// <summary>Shows the preferences.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ShowPreferencesAsync(IInteractionContext<PreferencesViewModel, RxVoid> context)
    {
        await new PreferencesWindow { ViewModel = context.Input }.ShowDialog(this);
        context.SetOutput(RxVoid.Default);
    }

    /// <summary>Shows the Search in Folder window, or brings it forward when it is already open.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private Task ShowFolderSearchAsync(IInteractionContext<FolderSearchViewModel, RxVoid> context)
    {
        if (_folderSearch is { } open)
        {
            open.Activate();
        }
        else
        {
            var window = new FolderSearchWindow { ViewModel = context.Input };
            _folderSearch = window;
            _folderSearchClosed = window.GetObservable(WindowClosedEvent, RoutingStrategies.Direct).SubscribeSafe(
                _ =>
                {
                    _folderSearch = null;
                    _folderSearchClosed?.Dispose();
                    _folderSearchClosed = null;
                },
                OnError);
            window.Show(this);
        }

        context.SetOutput(RxVoid.Default);
        return Task.CompletedTask;
    }

    /// <summary>Shows the properties dialog.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ShowPropertiesAsync(IInteractionContext<DocumentTabViewModel, RxVoid> context)
    {
        var dialog = new PropertiesWindow { ViewModel = new(context.Input) };
        await dialog.ShowDialog(this);
        context.SetOutput(RxVoid.Default);
    }

    /// <summary>Goes full screen with only the page showing while the selected tab presents, and back afterwards.</summary>
    /// <param name="presenting">Whether the selected tab is presenting.</param>
    private void OnPresentingChanged(bool presenting)
    {
        TabBar.IsVisible = !presenting;
        if (presenting && WindowState != WindowState.FullScreen)
        {
            _stateBeforePresenting = WindowState;
            WindowState = WindowState.FullScreen;
        }
        else if (!presenting && _stateBeforePresenting is { } previous)
        {
            WindowState = previous;
            _stateBeforePresenting = null;
        }
    }

    /// <summary>Opens dropped files, for example from Dolphin, or a dropped web address.</summary>
    /// <param name="e">The event.</param>
    private void OnDrop(DragEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (e.DataTransfer.TryGetFiles() is { Length: > 0 } files)
        {
            viewModel.Open(ToPaths(files));
        }
        else if (e.DataTransfer.TryGetText() is { Length: > 0 } text)
        {
            viewModel.Open(text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
    }

    /// <summary>Runs the command of a keyboard shortcut, then handles shortcuts that would otherwise move keyboard focus.</summary>
    /// <param name="e">The event.</param>
    private void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        if (Shortcuts.TryRun(viewModel, e))
        {
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && viewModel.SelectedTab is { IsPresenting: true } presenting)
        {
            presenting.SetPresenting(false);
            e.Handled = true;
            return;
        }

        var control = (e.KeyModifiers & KeyModifiers.Control) != 0;
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        switch (e.Key)
        {
            case Key.Tab when control:
            {
                _ = (shift ? viewModel.PreviousTabCommand : viewModel.NextTabCommand).Execute().Subscribe();
                e.Handled = true;
                break;
            }

            case Key.L when control:
            {
                FindDocumentView()?.FocusPageBox();
                e.Handled = true;
                break;
            }

            case Key.F11:
            {
                WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
                e.Handled = true;
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Finds the visible document view.</summary>
    /// <returns>The view, if any.</returns>
    private DocumentView? FindDocumentView()
    {
        foreach (var visual in DocumentHost.GetVisualDescendants())
        {
            if (visual is DocumentView view)
            {
                return view;
            }
        }

        return null;
    }

    /// <summary>Gets the tab under a pointer event.</summary>
    /// <param name="e">The event.</param>
    /// <returns>The tab, if any.</returns>
    private DocumentTabViewModel? GetTabAt(PointerEventArgs e)
    {
        var point = e.GetPosition(TabStrip);
        foreach (var visual in TabStrip.GetVisualsAt(point))
        {
            if (visual is Control { DataContext: DocumentTabViewModel tab })
            {
                return tab;
            }
        }

        return null;
    }

    /// <summary>Starts a possible tab drag, or closes the tab on middle click.</summary>
    /// <param name="e">The event.</param>
    private void OnTabPointerPressed(PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(TabStrip).Properties;
        var tab = GetTabAt(e);
        if (properties.IsMiddleButtonPressed && tab is not null)
        {
            if (ViewModel is { } viewModel)
            {
                _ = viewModel.CloseTabCommand.Execute(tab).Subscribe();
            }

            e.Handled = true;
            return;
        }

        if (!properties.IsLeftButtonPressed)
        {
            return;
        }

        _draggedTab = tab;
        _dragStart = e.GetPosition(TabStrip);
    }

    /// <summary>Reorders tabs while dragging.</summary>
    /// <param name="e">The event.</param>
    private void OnTabPointerMoved(PointerEventArgs e)
    {
        if (_draggedTab is null || ViewModel is not { } viewModel || Math.Abs(e.GetPosition(TabStrip).X - _dragStart.X) < DragThreshold)
        {
            return;
        }

        var target = GetTabAt(e);
        if (target is null || target == _draggedTab)
        {
            return;
        }

        viewModel.MoveTab(viewModel.Tabs.IndexOf(_draggedTab), viewModel.Tabs.IndexOf(target));
        _dragStart = e.GetPosition(TabStrip);
    }

    /// <summary>Scrolls the tab strip horizontally with the mouse wheel.</summary>
    /// <param name="e">The event.</param>
    private void OnTabWheel(PointerWheelEventArgs e)
    {
        if (TabStrip.FindDescendantOfType<ScrollViewer>() is not { } scroller)
        {
            return;
        }

        scroller.Offset = new(Math.Max(0, scroller.Offset.X - ((e.Delta.Y + e.Delta.X) * TabWheelStep)), scroller.Offset.Y);
        e.Handled = true;
    }
}
