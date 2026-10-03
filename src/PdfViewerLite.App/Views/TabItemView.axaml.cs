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

/// <summary>One tab in the tab strip, with its hover preview.</summary>
[DebuggerDisplay("{ViewModel}")]
public sealed partial class TabItemView : UserControl, IViewFor<DocumentTabViewModel>
{
    /// <summary>Defines the <see cref="ViewModel"/> property.</summary>
    public static readonly StyledProperty<DocumentTabViewModel?> ViewModelProperty = AvaloniaProperty.Register<TabItemView, DocumentTabViewModel?>(nameof(ViewModel));

    /// <summary>The bindings made while attached.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="TabItemView"/> class.</summary>
    public TabItemView()
    {
        InitializeComponent();
        Preview.Width = DocumentTabViewModel.PreviewWidth;
    }

    /// <inheritdoc/>
    public DocumentTabViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = value as DocumentTabViewModel;
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ViewModel = DataContext as DocumentTabViewModel;
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _bindings =
        [
            this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.NameText.Text),
            this.OneWayBind(ViewModel, static vm => vm.HasUnsavedChanges, static v => v.UnsavedDot.IsVisible),
            this.OneWayBind(ViewModel, static vm => vm.Title, static v => v.PreviewTitle.Text),
            this.OneWayBind(ViewModel, static vm => vm.PreviewCaption, static v => v.PreviewCaption.Text),
            this.OneWayBind(ViewModel, static vm => vm.FilePath, static v => v.PreviewPath.Text),
            this.OneWayBind(ViewModel, static vm => vm.PreviewPageIndex, static v => v.PreviewPage.PageIndex),
            this.OneWayBind(ViewModel, static vm => vm.PreviewHeight, static v => v.PreviewPage.Height),
            this.OneWayBind(ViewModel, static vm => vm.PreviewHeight, static v => v.PreviewPage.IsVisible, static height => height > 0),
            this.WhenAnyValue(static v => v.ViewModel).BindTo(this, static v => v.PreviewPage.Tab),
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
