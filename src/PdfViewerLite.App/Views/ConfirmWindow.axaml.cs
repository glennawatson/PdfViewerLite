// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>Asks before a destructive action; closes with <see langword="true"/> only when the user goes ahead.</summary>
[DebuggerDisplay("{Title}")]
public sealed partial class ConfirmWindow : ReactiveUI.Avalonia.ReactiveWindow<ConfirmRequest>
{
    /// <summary>The button subscriptions.</summary>
    private readonly MultipleDisposable _buttons;

    /// <summary>Initializes a new instance of the <see cref="ConfirmWindow"/> class.</summary>
    public ConfirmWindow()
    {
        InitializeComponent();
        _buttons =
        [
            ConfirmButton.GetObservable(Button.ClickEvent, RoutingStrategies.Bubble).SubscribeSafe(_ => Close(true), static error => Trace.TraceError(error.ToString())),
            CancelButton.GetObservable(Button.ClickEvent, RoutingStrategies.Bubble).SubscribeSafe(_ => Close(false), static error => Trace.TraceError(error.ToString())),
        ];
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property != ViewModelProperty || ViewModel is not { } request)
        {
            return;
        }

        // The request is immutable, so it is shown once rather than bound.
        Title = request.Title;
        TitleText.Text = request.Title;
        MessageText.Text = request.Message;
        ConfirmButton.Content = request.ConfirmText;
    }

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _ = CancelButton.Focus();
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _buttons.Dispose();
    }
}
