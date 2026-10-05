// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using PdfViewerLite.Core.Platform;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Views;

/// <summary>One recent document on the start page.</summary>
[DebuggerDisplay("RecentDocumentView: {ViewModel}")]
public sealed partial class RecentDocumentView : ReactiveUI.Avalonia.ReactiveUserControl<RecentDocument>
{
    /// <summary>Initializes a new instance of the <see cref="RecentDocumentView"/> class.</summary>
    public RecentDocumentView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            // A recent document is immutable and raises no change notifications, so the view follows only which one it shows.
            var recent = this.WhenChanged(static v => v.ViewModel);
            disposables.Add(recent.Select(static item => item?.FileName).BindTo(this, static v => v.NameText.Text));
            disposables.Add(recent.Select(static item => item?.Folder).BindTo(this, static v => v.FolderText.Text));

            // The tip is an attached property, which a binding expression cannot reach.
            disposables.Add(recent.SubscribeSafe(item => ToolTip.SetTip(this, item?.FilePath), static error => Trace.TraceError(error.ToString())));
        });
    }
}
