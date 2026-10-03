// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>One layer in the layers panel: a check box with the layer's name.</summary>
[DebuggerDisplay("{ViewModel}")]
public sealed partial class LayerItemView : UserControl, IViewFor<LayerItemViewModel>
{
    /// <summary>Defines the <see cref="ViewModel"/> property.</summary>
    public static readonly StyledProperty<LayerItemViewModel?> ViewModelProperty = AvaloniaProperty.Register<LayerItemView, LayerItemViewModel?>(nameof(ViewModel));

    /// <summary>The bindings made while loaded.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="LayerItemView"/> class.</summary>
    public LayerItemView() => InitializeComponent();

    /// <inheritdoc/>
    public LayerItemViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = value as LayerItemViewModel;
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ViewModel = DataContext as LayerItemViewModel;
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _bindings =
        [
            this.OneWayBind(ViewModel, static vm => vm.Name, static v => v.LayerBox.Content),
            this.Bind(ViewModel, static vm => vm.IsVisible, static v => v.LayerBox.IsChecked, static on => on, static on => on == true),
        ];
    }

    /// <inheritdoc/>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _bindings?.Dispose();
        _bindings = null;
    }
}
