// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PdfViewerLite.App.Views;

/// <summary>Shows document properties.</summary>
[DebuggerDisplay("{Title}")]
public sealed partial class PropertiesWindow : Window
{
    /// <summary>Initializes a new instance of the <see cref="PropertiesWindow"/> class.</summary>
    public PropertiesWindow() => InitializeComponent();

    /// <summary>Closes the window.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The event.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
