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

/// <summary>Asks for some text, for example a note; closes with the text, or <see langword="null"/> when cancelled.</summary>
[DebuggerDisplay("{Title}")]
public sealed partial class PromptWindow : ReactiveUI.Avalonia.ReactiveWindow<TextPrompt>
{
    /// <summary>The height of a multi-line text box.</summary>
    private const double MultilineHeight = 96;

    /// <summary>The height of a single line text box.</summary>
    private const double SingleLineHeight = 32;

    /// <summary>The button subscriptions.</summary>
    private readonly MultipleDisposable _buttons;

    /// <summary>Initializes a new instance of the <see cref="PromptWindow"/> class.</summary>
    public PromptWindow()
    {
        InitializeComponent();
        _buttons =
        [
            ConfirmButton.GetObservable(Button.ClickEvent, RoutingStrategies.Bubble).SubscribeSafe(_ => Close(InputBox.Text ?? string.Empty), static error => Trace.TraceError(error.ToString())),
            CancelButton.GetObservable(Button.ClickEvent, RoutingStrategies.Bubble).SubscribeSafe(_ => Close(null), static error => Trace.TraceError(error.ToString())),
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
        LabelText.Text = request.Label;
        InputBox.Text = request.Text;
        InputBox.AcceptsReturn = request.Multiline;
        InputBox.TextWrapping = request.Multiline ? Avalonia.Media.TextWrapping.Wrap : Avalonia.Media.TextWrapping.NoWrap;
        InputBox.MinHeight = request.Multiline ? MultilineHeight : SingleLineHeight;
        ConfirmButton.Content = request.AcceptText;
        ConfirmButton.IsDefault = !request.Multiline;
    }

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _ = InputBox.Focus();
        InputBox.SelectAll();
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _buttons.Dispose();
    }
}
