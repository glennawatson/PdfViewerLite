// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace PdfViewerLite.HyperPdf;

/// <content>
/// Optimising. The optimiser starts from a snapshot of the open document taken the way saving writes it: annotations
/// removed but kept are left out, and they stay in memory afterwards so removing them can still be undone.
/// </content>
internal sealed partial class HyperPdfAnnotations
{
    /// <summary>Takes a snapshot of the document with its unsaved edits, leaving out annotations removed but kept.</summary>
    /// <returns>The snapshot, or <see langword="null"/> when the document is closed.</returns>
    internal PdfDocument? OpenSnapshot()
    {
        lock (_gate)
        {
            if (_document.IsDisposed)
            {
                return null;
            }

            var hidden = HideRemoved();
            try
            {
                return _document.OpenWorkingCopy();
            }
            finally
            {
                Restore(hidden);
            }
        }
    }
}
