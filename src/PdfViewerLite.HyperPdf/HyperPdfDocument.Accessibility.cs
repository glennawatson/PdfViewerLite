// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;

namespace PdfViewerLite.HyperPdf;

/// <content>The document's accessibility report, for a view the app can show later.</content>
public sealed partial class HyperPdfDocument
{
    /// <summary>Reads the document's PDF/UA claim and common accessibility faults. This is a reading report, not a validator.</summary>
    /// <param name="cancellation">A token that stops the work between elements and pages.</param>
    /// <returns>The report.</returns>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal PdfAccessibilityReport GetAccessibilityReport(CancellationToken cancellation)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return _document.GetAccessibilityReport(cancellation);
    }
}
