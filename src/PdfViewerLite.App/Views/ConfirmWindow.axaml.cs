// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PdfViewerLite.App.Views;

/// <summary>Asks before a destructive action; closes with <see langword="true"/> only when the user goes ahead.</summary>
[DebuggerDisplay("{Title}")]
public sealed partial class ConfirmWindow : Window
{
    /// <summary>Initializes a new instance of the <see cref="ConfirmWindow"/> class.</summary>
    public ConfirmWindow() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _ = CancelButton.Focus();
    }

    /// <summary>Goes ahead with the action.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);

    /// <summary>Cancels the action.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
