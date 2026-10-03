// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>One annotation in the sidebar: its kind, page and note, with its colour beside the name.</summary>
[DebuggerDisplay("{ViewModel}")]
public sealed partial class AnnotationItemView : UserControl, IViewFor<AnnotationItemViewModel>
{
    /// <summary>Defines the <see cref="ViewModel"/> property.</summary>
    public static readonly StyledProperty<AnnotationItemViewModel?> ViewModelProperty = AvaloniaProperty.Register<AnnotationItemView, AnnotationItemViewModel?>(nameof(ViewModel));

    /// <summary>Opaque alpha in 0xAARRGGBB.</summary>
    private const uint OpaqueAlpha = 0xFF000000U;

    /// <summary>The bindings made while attached.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="AnnotationItemView"/> class.</summary>
    public AnnotationItemView() => InitializeComponent();

    /// <inheritdoc/>
    public AnnotationItemViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = value as AnnotationItemViewModel;
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ViewModel = DataContext as AnnotationItemViewModel;
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // An annotation item is a snapshot, replaced when the annotation changes, so the view follows which one it shows.
        _bindings = [this.WhenAnyValue(static v => v.ViewModel).SubscribeSafe(Show, static error => Trace.TraceError(error.ToString()))];
    }

    /// <inheritdoc/>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _bindings?.Dispose();
        _bindings = null;
    }

    /// <summary>Shows an annotation item.</summary>
    /// <param name="item">The item.</param>
    private void Show(AnnotationItemViewModel? item)
    {
        KindText.Text = item is null ? null : $"{item.KindName} · {item.PageCaption}";
        SummaryText.Text = item?.Summary;
        Swatch.Background = item is null ? null : new SolidColorBrush(Color.FromUInt32(OpaqueAlpha | item.Annotation.Color));
    }
}
