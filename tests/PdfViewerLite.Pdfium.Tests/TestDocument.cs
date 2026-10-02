// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>A generated PDF opened with PDFium, deleted on dispose.</summary>
internal sealed class TestDocument : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="TestDocument"/> class.</summary>
    /// <param name="pageCount">The page count.</param>
    internal TestDocument(int pageCount)
    {
        FilePath = TestPdf.WriteTempFile(pageCount);
        Document = new PdfiumEngine().Open(FilePath, null);
    }

    /// <summary>Gets the file path.</summary>
    internal string FilePath { get; }

    /// <summary>Gets the document.</summary>
    internal IDocument Document { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Document.Dispose();
        File.Delete(FilePath);
    }
}
