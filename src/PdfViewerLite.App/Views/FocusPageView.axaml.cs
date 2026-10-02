// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>One page of Focus Mode; its reading order is worked out when the page comes into view.</summary>
[DebuggerDisplay("{ViewModel}")]
public sealed partial class FocusPageView : ReactiveUI.Avalonia.ReactiveUserControl<FocusPageViewModel>
{
    /// <summary>The bindings made while loaded.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="FocusPageView"/> class.</summary>
    public FocusPageView()
    {
        InitializeComponent();
        BlockList.ItemTemplate = new FuncDataTemplate<FocusBlockViewModel>((_, _) => new FocusBlockView { FocusMode = FocusMode });
    }

    /// <summary>Gets or sets Focus Mode, handed to each block for Read from Here.</summary>
    public FocusModeViewModel? FocusMode { get; set; }

    /// <summary>Gets the block views currently shown.</summary>
    /// <returns>The views, top to bottom.</returns>
    internal List<FocusBlockView> BlockViews()
    {
        var views = new List<FocusBlockView>();
        foreach (var visual in BlockList.GetVisualDescendants())
        {
            if (visual is FocusBlockView view)
            {
                views.Add(view);
            }
        }

        return views;
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _bindings =
        [
            this.OneWayBind(ViewModel, static vm => vm.Label, static v => v.PageLabel.Text),
            this.OneWayBind(ViewModel, static vm => vm.Blocks, static v => v.BlockList.ItemsSource),
            this.OneWayBind(ViewModel, static vm => vm.IsEmpty, static v => v.EmptyText.IsVisible),
            this.WhenAnyValue(static v => v.ViewModel).SubscribeSafe(static page => _ = page?.LoadAsync(), static error => Trace.TraceError(error.ToString())),
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
