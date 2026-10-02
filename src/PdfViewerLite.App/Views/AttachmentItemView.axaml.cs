// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Interactivity;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Attachments;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>One embedded file in the attachments panel: its name and size.</summary>
[DebuggerDisplay("{ViewModel}")]
public sealed partial class AttachmentItemView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentAttachment>
{
    /// <summary>The bindings made while loaded.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="AttachmentItemView"/> class.</summary>
    public AttachmentItemView() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // An attachment is an immutable record, so the view follows which one it shows.
        _bindings = [this.WhenAnyValue(static v => v.ViewModel).SubscribeSafe(Show, static error => Trace.TraceError(error.ToString()))];
    }

    /// <inheritdoc/>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _bindings?.Dispose();
        _bindings = null;
    }

    /// <summary>Shows an attachment.</summary>
    /// <param name="attachment">The attachment.</param>
    private void Show(DocumentAttachment? attachment)
    {
        NameText.Text = attachment?.Name;
        SizeText.Text = attachment is null ? null : AttachmentsViewModel.FormatSize(attachment.Size);
    }
}
