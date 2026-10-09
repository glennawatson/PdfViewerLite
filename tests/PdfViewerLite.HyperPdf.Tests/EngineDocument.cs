// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Pdfium;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// A generated PDF opened with a named engine, deleted on dispose. The engine-parameterised suites run each PDFium
/// adapter scenario through it on both engines and compare the results with PDFium's.
/// </summary>
[DebuggerDisplay("EngineDocument: {Engine}")]
internal sealed class EngineDocument : IDisposable
{
    /// <summary>The PDFium engine's name.</summary>
    internal const string Pdfium = "PDFium";

    /// <summary>The HyperPDF engine's name.</summary>
    internal const string HyperPdf = "HyperPDF";

    /// <summary>Initializes a new instance of the <see cref="EngineDocument"/> class.</summary>
    /// <param name="engine">The engine name: <see cref="Pdfium"/> or <see cref="HyperPdf"/>.</param>
    /// <param name="bytes">The PDF bytes.</param>
    internal EngineDocument(string engine, byte[] bytes)
    {
        Engine = engine;
        FilePath = Path.Combine(Path.GetTempPath(), $"engine-suite-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(FilePath, bytes);
        Document = Open(engine, FilePath);
    }

    /// <summary>Gets the engine name.</summary>
    internal string Engine { get; }

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

    /// <summary>Opens a file with a named engine.</summary>
    /// <param name="engine">The engine name.</param>
    /// <param name="path">The file.</param>
    /// <returns>The document.</returns>
    internal static IDocument Open(string engine, string path) =>
        string.Equals(engine, HyperPdf, StringComparison.Ordinal) ? new HyperPdfEngine().Open(path, null) : new PdfiumEngine().Open(path, null);
}
