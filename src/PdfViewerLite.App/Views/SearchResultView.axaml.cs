// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using Avalonia.Interactivity;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>One search result in the sidebar.</summary>
[DebuggerDisplay("{ViewModel}")]
public sealed partial class SearchResultView : ReactiveUI.Avalonia.ReactiveUserControl<SearchResultItemViewModel>
{
    /// <summary>The bindings made while attached.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="SearchResultView"/> class.</summary>
    public SearchResultView() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // A search result is immutable, so the view follows only which one it shows.
        _bindings = [this.WhenAnyValue(static v => v.ViewModel).SubscribeSafe(Show, static error => Trace.TraceError(error.ToString()))];
    }

    /// <inheritdoc/>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _bindings?.Dispose();
        _bindings = null;
    }

    /// <summary>Shows a search result.</summary>
    /// <param name="result">The result.</param>
    private void Show(SearchResultItemViewModel? result)
    {
        PageText.Text = result is null ? null : string.Create(CultureInfo.CurrentCulture, $"Page {result.PageNumber}");
        ContextText.Text = result?.Context;
    }
}
