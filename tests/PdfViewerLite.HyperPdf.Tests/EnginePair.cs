// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Pdfium;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>The same file opened with PDFium and HyperPDF, deleted on dispose.</summary>
internal sealed class EnginePair : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="EnginePair"/> class.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    internal EnginePair(byte[] bytes)
    {
        FilePath = Path.Combine(Path.GetTempPath(), $"hyperpdf-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(FilePath, bytes);
        Pdfium = new PdfiumEngine().Open(FilePath, null);
        HyperPdf = new HyperPdfEngine().Open(FilePath, null);
    }

    /// <summary>Gets the file path.</summary>
    internal string FilePath { get; }

    /// <summary>Gets the document opened with PDFium.</summary>
    internal IDocument Pdfium { get; }

    /// <summary>Gets the document opened with HyperPDF.</summary>
    internal IDocument HyperPdf { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Pdfium.Dispose();
        HyperPdf.Dispose();
        File.Delete(FilePath);
    }
}
