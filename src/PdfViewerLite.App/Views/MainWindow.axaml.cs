// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
[DebuggerDisplay("{Title}")]
public sealed partial class MainWindow : Window
{
    /// <summary>How far the pointer must move before a tab drag starts.</summary>
    private const double DragThreshold = 6;

    /// <summary>Pixels the tab strip scrolls per wheel notch.</summary>
    private const double TabWheelStep = 60;

    /// <summary>The PDF file type filter.</summary>
    private static readonly FilePickerFileType PdfFileType = new("PDF documents") { Patterns = ["*.pdf", "*.PDF"], MimeTypes = ["application/pdf"] };

    /// <summary>Input subscriptions for the window's lifetime.</summary>
    private readonly MultipleDisposable _inputSubscriptions;

    /// <summary>Interaction handler registrations for the current view model.</summary>
    private MultipleDisposable? _registrations;

    /// <summary>The tab being dragged.</summary>
    private DocumentTabViewModel? _draggedTab;

    /// <summary>Where the tab drag started.</summary>
    private Point _dragStart;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
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
            TabFinderList.GetObservable(SelectingItemsControl.SelectionChangedEvent).SubscribeSafe(_ => OnTabFound(), OnError),
            Signal.FromEvent<EventHandler, RxVoid>(
                static handler => (_, _) => handler(RxVoid.Default),
                handler => TabFinderButton.Flyout!.Opened += handler,
                handler => TabFinderButton.Flyout!.Opened -= handler).SubscribeSafe(_ => OnTabFinderOpened(), OnError),
        ];
    }

    /// <summary>Gets the view model.</summary>
    public MainViewModel? ViewModel => DataContext as MainViewModel;

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
        _registrations?.Dispose();
        _registrations = null;
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        _registrations =
        [
            viewModel.TabFinderRequests.SubscribeSafe(_ => TabFinderButton.Flyout?.ShowAt(TabFinderButton), OnError),
            viewModel.OpenFileInteraction.RegisterHandler(OpenFilesAsync),
            viewModel.ShowPropertiesInteraction.RegisterHandler(ShowPropertiesAsync),
            viewModel.ConfirmInteraction.RegisterHandler(ConfirmAsync),
            viewModel.ShowPreferencesInteraction.RegisterHandler(ShowPreferencesAsync),
        ];
    }

    /// <inheritdoc/>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (ViewModel is not { } viewModel)
        {
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
        _registrations?.Dispose();
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

    /// <summary>Asks the user to confirm a destructive action.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ConfirmAsync(IInteractionContext<ConfirmRequest, bool> context)
    {
        var dialog = new ConfirmWindow { DataContext = context.Input };
        context.SetOutput(await dialog.ShowDialog<bool>(this));
    }

    /// <summary>Shows the preferences.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ShowPreferencesAsync(IInteractionContext<PreferencesViewModel, RxVoid> context)
    {
        await new PreferencesWindow { DataContext = context.Input }.ShowDialog(this);
        context.SetOutput(RxVoid.Default);
    }

    /// <summary>Shows the properties dialog.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ShowPropertiesAsync(IInteractionContext<DocumentTabViewModel, RxVoid> context)
    {
        var dialog = new PropertiesWindow { DataContext = new PropertiesViewModel(context.Input) };
        await dialog.ShowDialog(this);
        context.SetOutput(RxVoid.Default);
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

    /// <summary>Handles shortcuts that would otherwise move keyboard focus.</summary>
    /// <param name="e">The event.</param>
    private void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (ViewModel is not { } viewModel)
        {
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
            ViewModel?.CloseTab(tab);
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
