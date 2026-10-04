// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Platform;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.Views;

/// <summary>The start page: open a document, or pick a recent one.</summary>
[DebuggerDisplay("StartView")]
public sealed partial class StartView : UserControl, IViewFor<MainViewModel>
{
    /// <summary>Defines the <see cref="ViewModel"/> property.</summary>
    public static readonly StyledProperty<MainViewModel?> ViewModelProperty = AvaloniaProperty.Register<StartView, MainViewModel?>(nameof(ViewModel));

    /// <summary>The bindings made while attached.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="StartView"/> class.</summary>
    public StartView()
    {
        InitializeComponent();
        RecentList.ItemTemplate = new FuncDataTemplate<RecentDocument>(static (_, _) => new RecentDocumentView());
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

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ViewModel = DataContext as MainViewModel;
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // A recent document opens on a click or Enter, never just because the selection moved.
        var picks = Signal.Merge(
            RecentList.GetObservable(TappedEvent, RoutingStrategies.Bubble).Select(static _ => RxVoid.Default),
            RecentList.GetObservable(KeyDownEvent, RoutingStrategies.Bubble).Where(static args => args.Key == Key.Enter).Select(static _ => RxVoid.Default));
        _bindings =
        [
            this.BindCommand(ViewModel, static vm => vm.OpenCommand, static v => v.OpenButton),
            this.OneWayBind(ViewModel, static vm => vm.RecentDocuments, static v => v.RecentList.ItemsSource),
            this.OneWayBind(ViewModel, static vm => vm.RecentDocuments.Count, static v => v.RecentPanel.IsVisible, static count => count > 0),
            picks.Select(_ => RecentList.SelectedItem as RecentDocument).Where(static recent => recent is not null).Select(static recent => recent!)
                .InvokeCommand(ViewModel, static vm => vm.OpenRecentCommand),
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
