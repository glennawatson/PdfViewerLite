// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Raster;

namespace HyperPdfLibrary.Document;

/// <summary>Renders document pages to raster images.</summary>
public static class PdfDocumentRaster
{
    /// <summary>
    /// Reports which pages paint only images, the image filters, sizes and resolutions, whether there is an OCR text
    /// layer, and the file's PDF/R claim comment if it has one.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">The cancellation token, checked once per page.</param>
    /// <returns>The report.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    public static PdfRasterReport GetRasterReport(PdfDocument document, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        return RasterReportBuilder.Build(document, cancellationToken);
    }
}
