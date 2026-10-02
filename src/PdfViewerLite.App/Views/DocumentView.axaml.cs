// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;

namespace PdfViewerLite.App.Views;

/// <summary>Shows one document: tool bar, find bar, sidebar and pages.</summary>
[DebuggerDisplay("{DataContext}")]
public sealed partial class DocumentView : UserControl
{
    /// <summary>The tab currently wired up.</summary>
    private DocumentTabViewModel? _tab;

    /// <summary>Initializes a new instance of the <see cref="DocumentView"/> class.</summary>
    public DocumentView()
    {
        InitializeComponent();
        ThumbnailList.AddHandler(ScrollViewer.ScrollChangedEvent, OnThumbnailsScrolled);
    }

    /// <summary>Focuses the page box.</summary>
    public void FocusPageBox()
    {
        _ = PageBox.Focus();
        PageBox.SelectAll();
    }

    /// <summary>Copies the selected text.</summary>
    /// <returns>The selected text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetSelectedText() => Canvas.GetSelectedText();

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_tab is not null)
        {
            _tab.UriRequested -= OnUriRequested;
            _tab.Search.PropertyChanged -= OnSearchPropertyChanged;
        }

        _tab = DataContext as DocumentTabViewModel;
        if (_tab is null)
        {
            return;
        }

        _tab.UriRequested += OnUriRequested;
        _tab.Search.PropertyChanged += OnSearchPropertyChanged;
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(FocusControl, Canvas, DispatcherPriority.Loaded);
    }

    /// <summary>Focuses a control passed as dispatcher state.</summary>
    /// <param name="state">The control.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FocusControl(object? state) => (state as Control)?.Focus();

    /// <summary>Opens external links with the desktop's default handler.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="uri">The URI.</param>
    private void OnUriRequested(object? sender, Uri uri)
    {
        if (uri.Scheme is "http" or "https" or "mailto" && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(uri);
        }
    }

    /// <summary>Focuses the find box when the find bar opens.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event.</param>
    private void OnSearchPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchViewModel.IsOpen) && _tab?.Search.IsOpen == true)
        {
            Dispatcher.UIThread.Post(FocusControl, SearchBox, DispatcherPriority.Loaded);
        }
    }

    /// <summary>Drops thumbnail requests for items scrolled out of view and re-requests the visible ones.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event.</param>
    private void OnThumbnailsScrolled(object? sender, ScrollChangedEventArgs e)
    {
        if (_tab is null || e.OffsetDelta.Y == 0)
        {
            return;
        }

        _ = _tab.ThumbnailClient.Advance();
        foreach (var visual in ThumbnailList.GetVisualDescendants())
        {
            if (visual is PageThumbnail thumbnail)
            {
                thumbnail.InvalidateVisual();
            }
        }
    }
}
