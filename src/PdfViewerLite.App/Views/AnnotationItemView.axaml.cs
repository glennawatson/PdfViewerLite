// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Interactivity;
using Avalonia.Media;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>One annotation in the sidebar: its kind, page and note, with its colour beside the name.</summary>
[DebuggerDisplay("{ViewModel}")]
public sealed partial class AnnotationItemView : ReactiveUI.Avalonia.ReactiveUserControl<AnnotationItemViewModel>
{
    /// <summary>Opaque alpha in 0xAARRGGBB.</summary>
    private const uint OpaqueAlpha = 0xFF000000U;

    /// <summary>The bindings made while attached.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="AnnotationItemView"/> class.</summary>
    public AnnotationItemView() => InitializeComponent();

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
        KindText.Text = item?.Heading;
        SummaryText.Text = item?.Summary;
        RepliesText.Text = item?.RepliesText;
        RepliesText.IsVisible = item is { RepliesText.Length: > 0 };
        Swatch.Background = item is null ? null : new SolidColorBrush(Color.FromUInt32(OpaqueAlpha | item.Annotation.Color));
    }
}
