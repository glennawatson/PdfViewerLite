// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Editing;
using HyperPdfLibrary.Tests.Writing;
using HyperPdfLibrary.Writing;
using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// Edits pages and metadata with HyperPDF, saves incrementally or compactly, and checks PDFium and HyperPDF read the
/// same page count, page order (by PDFium's page text), page sizes after rotation, and metadata.
/// </summary>
public sealed class PageEditingParityTests
{
    /// <summary>A quarter turn.</summary>
    private const int QuarterTurn = 90;

    /// <summary>The index of the page deleted after reversing.</summary>
    private const int DeletedIndex = 3;

    /// <summary>The flat document's second page.</summary>
    private const int FlatSecondPage = 1;

    /// <summary>Pages in the flat documents.</summary>
    private const int FlatPages = 3;

    /// <summary>The last page of the flat documents.</summary>
    private const int FlatLastPage = 2;

    /// <summary>The structured document's last page index.</summary>
    private const int LastPage = 5;

    /// <summary>The structured document's fifth page index.</summary>
    private const int FifthPage = 4;

    /// <summary>The structured document's fourth page index.</summary>
    private const int FourthPage = 3;

    /// <summary>The structured document's third page index.</summary>
    private const int ThirdPage = 2;

    /// <summary>The title written.</summary>
    private const string Title = "Edited title";

    /// <summary>The author written.</summary>
    private const string Author = "Edited author";

    /// <summary>Reorder, delete, insert, rotate and metadata edits read back the same in both engines.</summary>
    /// <param name="incremental">Whether to save incrementally rather than rewrite.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task EditedDocumentMatchesInBothEngines(bool incremental)
    {
        using var insert = PdfDocument.Open(EditingTestDocuments.CreateFlat(FlatPages), null);
        using var document = PdfDocument.Open(EditingTestDocuments.CreateStructured(), null);
        document.ReorderPages([LastPage, FifthPage, FourthPage, ThirdPage, 1, 0]);
        document.DeletePages([DeletedIndex]);
        document.InsertPages(0, insert, [FlatSecondPage]);
        document.RotatePages([1], QuarterTurn);
        document.SetMetadata(new() { Title = Title, Author = Author });

        var saved = incremental ? PdfIncrementalWriter.Save(document.Objects) : PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);
        await AssertParity(saved, EditingTestDocuments.Expected("2 6 5 4 2 1"));
    }

    /// <summary>An encrypted document stays encrypted and reads back the same in both engines.</summary>
    /// <param name="incremental">Whether to save incrementally rather than rewrite.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task EncryptedEditsMatchInBothEngines(bool incremental)
    {
        using var document = PdfDocument.Open(WritingTestDocuments.Encrypt(EditingTestDocuments.CreateFlat(FlatPages)), null);
        document.MovePages([FlatLastPage], 0);
        document.SetRotation(0, QuarterTurn);
        document.SetMetadata(new() { Title = Title, Author = Author });

        var saved = incremental ? PdfIncrementalWriter.Save(document.Objects) : PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);
        using (var reopened = PdfDocument.Open(saved, null))
        {
            await Assert.That(reopened.IsEncrypted).IsTrue();
        }

        await AssertParity(saved, EditingTestDocuments.Expected("3 1 2"));
    }

    /// <summary>Opens saved bytes with both engines and compares them.</summary>
    /// <param name="saved">The saved file.</param>
    /// <param name="texts">The expected text of each page.</param>
    /// <returns>A task.</returns>
    private static async Task AssertParity(byte[] saved, string[] texts)
    {
        using var pair = new EnginePair(saved);
        await Assert.That(pair.Pdfium.PageCount).IsEqualTo(texts.Length);
        await Assert.That(pair.HyperPdf.PageCount).IsEqualTo(texts.Length);
        await Assert.That(PdfiumTexts(pair.Pdfium)).IsEquivalentTo(texts);
        using (var managed = PdfDocument.Open(saved, null))
        {
            await Assert.That(EditingTestDocuments.PageTexts(managed)).IsEquivalentTo(texts);
        }

        await Assert.That(pair.HyperPdf.GetPageSizes()).IsEquivalentTo(pair.Pdfium.GetPageSizes());
        var pdfium = pair.Pdfium.GetMetadata();
        var hyperPdf = pair.HyperPdf.GetMetadata();
        await Assert.That(pdfium.Title).IsEqualTo(Title);
        await Assert.That(pdfium.Author).IsEqualTo(Author);
        await Assert.That(hyperPdf.Title).IsEqualTo(pdfium.Title);
        await Assert.That(hyperPdf.Author).IsEqualTo(pdfium.Author);
    }

    /// <summary>Reads each page's text with PDFium.</summary>
    /// <param name="document">The PDFium document.</param>
    /// <returns>The trimmed text of each page.</returns>
    private static string[] PdfiumTexts(IDocument document)
    {
        var texts = new string[document.PageCount];
        for (var i = 0; i < texts.Length; i++)
        {
            texts[i] = document.GetText(i, 0, document.GetCharacterCount(i)).Trim();
        }

        return texts;
    }
}
