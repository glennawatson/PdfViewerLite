// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;

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

    /// <summary>Interaction handler registrations.</summary>
    private readonly List<IDisposable> _registrations = [];

    /// <summary>The tab being dragged.</summary>
    private DocumentTabViewModel? _draggedTab;

    /// <summary>Where the tab drag started.</summary>
    private Point _dragStart;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        TabStrip.AddHandler(PointerPressedEvent, OnTabPointerPressed, RoutingStrategies.Tunnel);
        TabStrip.AddHandler(PointerMovedEvent, OnTabPointerMoved, RoutingStrategies.Tunnel);
        TabStrip.AddHandler(PointerReleasedEvent, OnTabPointerReleased, RoutingStrategies.Tunnel);
        TabStrip.AddHandler(PointerWheelChangedEvent, OnTabWheel, RoutingStrategies.Tunnel);
        Closing += OnClosing;
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
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        _registrations.Clear();
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        _registrations.Add(viewModel.OpenFileInteraction.RegisterHandler(OpenFilesAsync));
        _registrations.Add(viewModel.ShowPropertiesInteraction.RegisterHandler(ShowPropertiesAsync));
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

    /// <summary>Accepts dragged files.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event.</param>
    private static void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) || e.DataTransfer.Contains(DataFormat.Text) ? DragDropEffects.Copy : DragDropEffects.None;

    /// <summary>Shows the open dialog; on KDE this goes through the XDG portal and shows the KDE file dialog.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task OpenFilesAsync(IInteractionContext<RxVoid, IReadOnlyList<string>> context)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Open Document", AllowMultiple = true, FileTypeFilter = [PdfFileType, FilePickerFileTypes.All] });
        context.SetOutput(ToPaths(files));
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
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event.</param>
    private void OnDrop(object? sender, DragEventArgs e)
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
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event.</param>
    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
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
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event.</param>
    private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
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
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event.</param>
    private void OnTabPointerMoved(object? sender, PointerEventArgs e)
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

    /// <summary>Ends a tab drag.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event.</param>
    private void OnTabPointerReleased(object? sender, PointerReleasedEventArgs e) => _draggedTab = null;

    /// <summary>Scrolls the tab strip horizontally with the mouse wheel.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event.</param>
    private void OnTabWheel(object? sender, PointerWheelEventArgs e)
    {
        if (TabStrip.FindDescendantOfType<ScrollViewer>() is not { } scroller)
        {
            return;
        }

        scroller.Offset = new(Math.Max(0, scroller.Offset.X - ((e.Delta.Y + e.Delta.X) * TabWheelStep)), scroller.Offset.Y);
        e.Handled = true;
    }

    /// <summary>Saves the session when the window closes.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnClosing(object? sender, WindowClosingEventArgs e) => ViewModel?.SaveSession();
}
