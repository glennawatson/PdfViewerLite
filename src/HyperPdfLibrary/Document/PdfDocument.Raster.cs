// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Raster;

namespace HyperPdfLibrary.Document;

/// <content>Raster-only (PDF/R shaped) file detection.</content>
public sealed partial class PdfDocument
{
    /// <summary>
    /// Reports which pages paint only images, the image filters, sizes and resolutions, whether there is an OCR text
    /// layer, and the file's PDF/R claim comment if it has one.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token, checked once per page.</param>
    /// <returns>The report.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    public PdfRasterReport GetRasterReport(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return RasterReportBuilder.Build(this, cancellationToken);
    }
}
