// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;

namespace PdfViewerLite.App.Views;

/// <summary>The start page shown when no document is open.</summary>
[DebuggerDisplay("StartView")]
public sealed partial class StartView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="StartView"/> class.</summary>
    public StartView() => InitializeComponent();
}
