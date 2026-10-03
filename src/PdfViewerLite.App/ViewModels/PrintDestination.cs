// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.App.ViewModels;

/// <summary>Where a print goes.</summary>
public enum PrintDestination
{
    /// <summary>A printer, chosen in the desktop's print dialog.</summary>
    Printer = 0,

    /// <summary>A PDF file, chosen in a save dialog.</summary>
    SaveAsPdf = 1,
}
