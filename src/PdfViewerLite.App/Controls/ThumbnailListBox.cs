// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.ObservableEvents;

namespace PdfViewerLite.App.Controls;

/// <summary>Keeps visible thumbnails still and follows offscreen selections by a viewport at a time.</summary>
[DebuggerDisplay("Selected thumbnail: {SelectedIndex}")]
public sealed class ThumbnailListBox : ListBox
{
    /// <summary>The tolerance for unchanged layout coordinates.</summary>
    private const double OffsetTolerance = 1e-6;

    /// <summary>The offset before an unrealized selection is brought into view.</summary>
    private double? _pendingOffset;

    /// <summary>Selection subscriptions while attached.</summary>
    private MultipleDisposable? _subscriptions;

    /// <summary>Initializes a new instance of the <see cref="ThumbnailListBox"/> class.</summary>
    public ThumbnailListBox()
    {
        AutoScrollToSelectedItem = false;

        // These live as long as the control and only reference it, so they need no owner.
        _ = this.Events().AttachedToVisualTree.SubscribeSafe(_ => Attach(), OnError);
        _ = this.Events().DetachedFromVisualTree.SubscribeSafe(_ => Detach(), OnError);
    }

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(ListBox);

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        if (_pendingOffset is { } offset && FollowSelection(offset))
        {
            _pendingOffset = null;
        }

        return result;
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Follows selection changes while attached.</summary>
    private void Attach() =>
        _subscriptions =
        [
            this.WhenChanged(static x => x.SelectedIndex).Skip(1).SubscribeSafe(_ => OnSelectionChanged(), OnError),
        ];

    /// <summary>Stops following the selection.</summary>
    private void Detach()
    {
        _subscriptions?.Dispose();
        _subscriptions = null;
        _pendingOffset = null;
    }

    /// <summary>Follows a newly selected thumbnail after it has been realized.</summary>
    private void OnSelectionChanged()
    {
        _pendingOffset = null;
        if (SelectedIndex < 0 || Scroll is not ScrollViewer scroll || scroll.Viewport.Height <= 0 || FollowSelection(scroll.Offset.Y))
        {
            return;
        }

        var offset = scroll.Offset.Y;
        _pendingOffset = offset;
        ScrollIntoView(SelectedIndex);
        if (FollowSelection(offset))
        {
            _pendingOffset = null;
        }
    }

    /// <summary>Moves whole viewports only when the selected item is outside the visible area.</summary>
    /// <param name="offset">The offset before the selection changed.</param>
    /// <returns>Whether the selected container has been realized.</returns>
    private bool FollowSelection(double offset)
    {
        if (Scroll is not ScrollViewer scroll || ContainerFromIndex(SelectedIndex) is not { } container
            || container.TranslatePoint(default, scroll) is not { } position)
        {
            return false;
        }

        var top = position.Y + scroll.Offset.Y;
        var next = ThumbnailViewport.GetOffset(offset, scroll.Viewport.Height, top, top + container.Bounds.Height, scroll.Extent.Height);
        if (Math.Abs(next - scroll.Offset.Y) > OffsetTolerance)
        {
            scroll.Offset = new(scroll.Offset.X, next);
        }

        return true;
    }
}
