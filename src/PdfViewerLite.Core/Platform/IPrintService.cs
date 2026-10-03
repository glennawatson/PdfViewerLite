// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Core.Platform;

/// <summary>Prints a PDF file through the desktop, which shows its own print dialog.</summary>
public interface IPrintService
{
    /// <summary>Gets a value indicating whether printing is available on this desktop.</summary>
    bool IsAvailable { get; }

    /// <summary>Hands a PDF file to the desktop's print dialog.</summary>
    /// <param name="filePath">The PDF to print; it must stay on disk until the task completes.</param>
    /// <param name="title">The job title, normally the document's file name.</param>
    /// <param name="cancellationToken">Cancels waiting for the desktop.</param>
    /// <returns><see langword="true"/> when the desktop accepted the file.</returns>
    Task<bool> PrintAsync(string filePath, string title, CancellationToken cancellationToken);
}
