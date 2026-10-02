// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>Shows one document: tool bar, find bar, sidebar and pages.</summary>
[DebuggerDisplay("{DataContext}")]
public sealed partial class DocumentView : UserControl
{
    /// <summary>The tab currently wired up.</summary>
    private DocumentTabViewModel? _tab;

    /// <summary>Subscriptions to the current tab.</summary>
    private MultipleDisposable? _tabSubscriptions;

    /// <summary>Subscriptions that live while the view is attached.</summary>
    private MultipleDisposable? _viewSubscriptions;

    /// <summary>Initializes a new instance of the <see cref="DocumentView"/> class.</summary>
    public DocumentView() => InitializeComponent();

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
        _tabSubscriptions?.Dispose();
        _tabSubscriptions = null;
        _tab = DataContext as DocumentTabViewModel;
        if (_tab is not { } tab)
        {
            return;
        }

        _tabSubscriptions =
            [
                tab.UriRequests.SubscribeSafe(OpenUri, OnError),
                tab.Search.WhenAnyValue(static x => x.IsOpen).Where(static open => open).SubscribeSafe(_ => Dispatcher.UIThread.Post(FocusControl, SearchBox, DispatcherPriority.Loaded), OnError),
            ];
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewSubscriptions =
        [
            ThumbnailList.GetObservable(ScrollViewer.ScrollChangedEvent)
                .Where(static args => args.OffsetDelta.Y != 0)
                .SubscribeSafe(_ => OnThumbnailsScrolled(), OnError),
        ];
        Dispatcher.UIThread.Post(FocusControl, Canvas, DispatcherPriority.Loaded);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _viewSubscriptions?.Dispose();
        _viewSubscriptions = null;
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Focuses a control passed as dispatcher state.</summary>
    /// <param name="state">The control.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FocusControl(object? state) => (state as Control)?.Focus();

    /// <summary>Opens external links with the desktop's default handler.</summary>
    /// <param name="uri">The URI.</param>
    private void OpenUri(Uri uri)
    {
        if (uri.Scheme is "http" or "https" or "mailto" && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(uri);
        }
    }

    /// <summary>Drops thumbnail requests for items scrolled out of view and re-requests the visible ones.</summary>
    private void OnThumbnailsScrolled()
    {
        if (_tab is null)
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
