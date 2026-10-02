// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PdfViewerLite.Core.Platform;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>One recent document on the start page.</summary>
[DebuggerDisplay("{ViewModel}")]
public sealed partial class RecentDocumentView : ReactiveUI.Avalonia.ReactiveUserControl<RecentDocument>
{
    /// <summary>The bindings made while attached.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="RecentDocumentView"/> class.</summary>
    public RecentDocumentView() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // A recent document is immutable, so the view follows only which one it shows.
        _bindings = [this.WhenAnyValue(static v => v.ViewModel).SubscribeSafe(Show, static error => Trace.TraceError(error.ToString()))];
    }

    /// <inheritdoc/>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _bindings?.Dispose();
        _bindings = null;
    }

    /// <summary>Shows a recent document.</summary>
    /// <param name="recent">The document.</param>
    private void Show(RecentDocument? recent)
    {
        NameText.Text = recent?.FileName;
        FolderText.Text = recent?.Folder;
        ToolTip.SetTip(this, recent?.FilePath);
    }
}
