// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>One page thumbnail in the sidebar.</summary>
[DebuggerDisplay("{ViewModel}")]
public sealed partial class ThumbnailItemView : UserControl, IViewFor<ThumbnailItemViewModel>
{
    /// <summary>Defines the <see cref="ViewModel"/> property.</summary>
    public static readonly StyledProperty<ThumbnailItemViewModel?> ViewModelProperty = AvaloniaProperty.Register<ThumbnailItemView, ThumbnailItemViewModel?>(nameof(ViewModel));

    /// <summary>Defines the <see cref="Tab"/> property.</summary>
    public static readonly StyledProperty<DocumentTabViewModel?> TabProperty = AvaloniaProperty.Register<ThumbnailItemView, DocumentTabViewModel?>(nameof(Tab));

    /// <summary>The bindings made while attached.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="ThumbnailItemView"/> class.</summary>
    public ThumbnailItemView() => InitializeComponent();

    /// <inheritdoc/>
    public ThumbnailItemViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = value as ThumbnailItemViewModel;
    }

    /// <summary>Gets or sets the tab the page belongs to.</summary>
    public DocumentTabViewModel? Tab
    {
        get => GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ViewModel = DataContext as ThumbnailItemViewModel;
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // A thumbnail item is immutable, so the view follows only which one it shows, and which tab it belongs to.
        _bindings =
        [
            this.WhenAnyValue(static v => v.ViewModel).SubscribeSafe(Show, static error => Trace.TraceError(error.ToString())),
            this.WhenAnyValue(static v => v.Tab).BindTo(this, static v => v.Thumbnail.Tab),
        ];
    }

    /// <inheritdoc/>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _bindings?.Dispose();
        _bindings = null;
    }

    /// <summary>Shows a thumbnail item.</summary>
    /// <param name="item">The item.</param>
    private void Show(ThumbnailItemViewModel? item)
    {
        Thumbnail.Width = item?.Width ?? 0;
        Thumbnail.Height = item?.Height ?? 0;
        Thumbnail.PageIndex = item?.PageIndex ?? -1;
        LabelText.Text = item?.Label;
    }
}
