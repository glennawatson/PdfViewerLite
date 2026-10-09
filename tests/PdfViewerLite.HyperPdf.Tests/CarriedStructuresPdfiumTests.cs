// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Editing;
using HyperPdfLibrary.Writing;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// Merges and extracts pages with HyperPDF and reopens the result with PDFium, which must see the copied forms,
/// outlines, links, page labels and layers.
/// </summary>
public sealed class CarriedStructuresPdfiumTests
{
    /// <summary>The first page's index.</summary>
    private const int FirstPage = 0;

    /// <summary>The second page's index.</summary>
    private const int SecondPage = 1;

    /// <summary>The third page's index.</summary>
    private const int ThirdPage = 2;

    /// <summary>The fourth page's index.</summary>
    private const int FourthPage = 3;

    /// <summary>The pages in a merge of two books.</summary>
    private const int MergedPages = 4;

    /// <summary>The layers in a merge of two books.</summary>
    private const int MergedLayers = 2;

    /// <summary>The label of the third page of a merge: the copy keeps the source's lower-case Roman numerals.</summary>
    private const string ThirdPageLabel = "i";

    /// <summary>Gets the names of the fields of a merge of two books, page by page.</summary>
    private static string[][] MergedFields => [["Name", "Address.Street"], ["Address.City"], ["Name_1", "Address_1.Street"], ["Address_1.City"]];

    /// <summary>A merge of two books reads back in PDFium with unique fields, outline entries, links, labels and layers.</summary>
    /// <param name="incremental">Whether to save incrementally rather than rewrite.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task MergedBooksReadBackInPdfium(bool incremental)
    {
        using var source = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        using var target = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        PdfDocumentPageOperations.InsertPages(target, target.PageCount, source, [FirstPage, SecondPage]);

        var saved = incremental ? PdfIncrementalWriter.Save(target.Objects) : PdfCompactWriter.Save(target.Objects, PdfCompactOptions.Default);
        using var pair = new EnginePair(saved);
        await Assert.That(pair.Pdfium.PageCount).IsEqualTo(MergedPages);
        await Assert.That(OutlinePages(pair.Pdfium)).IsEquivalentTo([FirstPage, SecondPage, ThirdPage, FourthPage]);
        await Assert.That(LinkPages(pair.Pdfium, ThirdPage)).IsEquivalentTo([FourthPage, FourthPage]);
        await Assert.That(LinkPages(pair.Pdfium, FourthPage)).IsEquivalentTo([ThirdPage]);
        await Assert.That(pair.Pdfium.GetPageLabel(ThirdPage)).IsEqualTo(ThirdPageLabel);
        await Assert.That(((ILayerSource)pair.Pdfium).GetLayers().Count).IsEqualTo(MergedLayers);
        await AssertFields(pair.Pdfium);
        await AssertFields(pair.HyperPdf);
    }

    /// <summary>Extracted pages read back in PDFium with their form, outline, links and layer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtractedPagesReadBackInPdfium()
    {
        using var source = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
        using var pair = new EnginePair(PdfDocumentPageOperations.ExtractPages(source, [FirstPage, SecondPage]));
        await Assert.That(pair.Pdfium.PageCount).IsEqualTo(CarryTestDocuments.BookPages);
        await Assert.That(OutlinePages(pair.Pdfium)).IsEquivalentTo([FirstPage, SecondPage]);
        await Assert.That(LinkPages(pair.Pdfium, FirstPage)).IsEquivalentTo([SecondPage, SecondPage]);
        await Assert.That(LinkPages(pair.Pdfium, SecondPage)).IsEquivalentTo([FirstPage]);
        await Assert.That(((ILayerSource)pair.Pdfium).GetLayers().Count).IsEqualTo(1);
        await Assert.That(((IFormFiller)pair.Pdfium).HasForm).IsTrue();
    }

    /// <summary>Gets the pages the top-level outline entries lead to.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The pages.</returns>
    private static int[] OutlinePages(IDocument document)
    {
        var pages = new List<int>();
        foreach (var node in document.GetOutline())
        {
            pages.Add(node.Target.PageIndex);
        }

        return [.. pages];
    }

    /// <summary>Gets the pages the links of a page lead to.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The pages.</returns>
    private static int[] LinkPages(IDocument document, int pageIndex)
    {
        var pages = new List<int>();
        foreach (var link in document.GetLinks(pageIndex))
        {
            pages.Add(link.Target.PageIndex);
        }

        return [.. pages];
    }

    /// <summary>Checks the full field names on each page of a merge.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    private static async Task AssertFields(IDocument document)
    {
        var filler = (IFormFiller)document;
        for (var page = 0; page < MergedPages; page++)
        {
            var fields = new List<FormField>();
            filler.GetFields(page, fields);
            await Assert.That(fields.ConvertAll(static field => field.Name)).IsEquivalentTo(MergedFields[page]);
        }
    }
}
