// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Writing;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Writes metadata text with HyperPDF into a PDF 1.x file and checks PDFium and HyperPDF read the same text back.</summary>
public sealed class TextStringParityTests
{
    /// <summary>Pages in the test document.</summary>
    private const int Pages = 1;

    /// <summary>A title that needs UTF-16 in a PDF 1.x file.</summary>
    private const string UnicodeTitle = "Café Ω €";

    /// <summary>A title that fits PDFDocEncoding.</summary>
    private const string LatinTitle = "Café € •";

    /// <summary>Text that fits PDFDocEncoding but replaces a UTF-16 title.</summary>
    private const string PlainTitle = "Plain";

    /// <summary>New text is read back identically by both engines, for UTF-16 and PDFDocEncoding.</summary>
    /// <param name="title">The title written.</param>
    /// <param name="incremental">Whether to save incrementally rather than rewrite.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(UnicodeTitle, true)]
    [Arguments(UnicodeTitle, false)]
    [Arguments(LatinTitle, true)]
    [Arguments(LatinTitle, false)]
    public async Task NewTitleReadsBackInBothEngines(string title, bool incremental)
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = title });

        await AssertTitle(Save(document, incremental), title);
    }

    /// <summary>A plain title replacing a UTF-16 title stays UTF-16 and still reads back in both engines.</summary>
    /// <param name="incremental">Whether to save incrementally rather than rewrite.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ReplacedUtf16TitleReadsBackInBothEngines(bool incremental)
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = UnicodeTitle });
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = PlainTitle });

        await AssertTitle(Save(document, incremental), PlainTitle);
    }

    /// <summary>Saves a document with the chosen writer.</summary>
    /// <param name="document">The document.</param>
    /// <param name="incremental">Whether to append an update.</param>
    /// <returns>The file.</returns>
    private static byte[] Save(PdfDocument document, bool incremental) =>
        incremental ? PdfIncrementalWriter.Save(document.Objects) : PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);

    /// <summary>Opens a file with both engines and checks the title.</summary>
    /// <param name="saved">The file.</param>
    /// <param name="title">The expected title.</param>
    /// <returns>A task.</returns>
    private static async Task AssertTitle(byte[] saved, string title)
    {
        using var pair = new EnginePair(saved);
        await Assert.That(pair.Pdfium.GetMetadata().Title).IsEqualTo(title);
        await Assert.That(pair.HyperPdf.GetMetadata().Title).IsEqualTo(title);
    }
}
