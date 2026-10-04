// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>One outline entry; keeps the tree item's expansion in step with the view model both ways.</summary>
[DebuggerDisplay("{ViewModel}")]
public sealed partial class OutlineItemView : UserControl, IViewFor<OutlineItemViewModel>
{
    /// <summary>Defines the <see cref="ViewModel"/> property.</summary>
    public static readonly StyledProperty<OutlineItemViewModel?> ViewModelProperty = AvaloniaProperty.Register<OutlineItemView, OutlineItemViewModel?>(nameof(ViewModel));

    /// <summary>The bindings made while attached.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="OutlineItemView"/> class.</summary>
    public OutlineItemView() => InitializeComponent();

    /// <inheritdoc/>
    public OutlineItemViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = value as OutlineItemViewModel;
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ViewModel = DataContext as OutlineItemViewModel;
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _bindings =
        [
            this.OneWayBind(ViewModel, static vm => vm.Title, static v => v.TitleText.Text),
            this.WhenAnyValue(static v => v.ViewModel).SubscribeSafe(vm => ToolTip.SetTip(this, vm?.Title), static error => Trace.TraceError(error.ToString())),
        ];
        if (this.FindAncestorOfType<TreeViewItem>() is not { } container)
        {
            return;
        }

        _bindings.Add(this.WhenAnyValue(static v => v.ViewModel!.IsExpanded).SubscribeSafe(expanded => container.IsExpanded = expanded, static error => Trace.TraceError(error.ToString())));
        _bindings.Add(container.GetObservable(TreeViewItem.IsExpandedProperty).SubscribeSafe(Expand, static error => Trace.TraceError(error.ToString())));
    }

    /// <inheritdoc/>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _bindings?.Dispose();
        _bindings = null;
    }

    /// <summary>Records the tree item's expansion in the view model.</summary>
    /// <param name="expanded">Whether the item is expanded.</param>
    private void Expand(bool expanded)
    {
        if (ViewModel is { } viewModel)
        {
            viewModel.IsExpanded = expanded;
        }
    }
}
