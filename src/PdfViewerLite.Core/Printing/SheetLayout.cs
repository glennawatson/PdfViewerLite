// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.Core.Printing;

/// <summary>How exported pages are laid onto sheets.</summary>
/// <param name="PagesPerSheet">Pages on each sheet: 1, 2, 4, 6, 9 or 16.</param>
/// <param name="Paper">The paper used when several pages share a sheet.</param>
/// <param name="IncludeAnnotations">Whether notes, highlights and drawings are printed; filled form fields always are.</param>
[DebuggerDisplay("{PagesPerSheet} per sheet on {Paper}")]
public readonly record struct SheetLayout(int PagesPerSheet, PaperSize Paper, bool IncludeAnnotations)
{
    /// <summary>Gets one page per sheet with annotations.</summary>
    public static SheetLayout Default => new(1, PaperSize.A4, true);
}
