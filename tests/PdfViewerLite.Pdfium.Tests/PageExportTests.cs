// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Printing;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests for exporting chosen pages through <see cref="IPageExporter"/>.</summary>
public sealed class PageExportTests
{
    /// <summary>The printed sides of a booklet of three pages: one sheet, both sides.</summary>
    private const int BookletSheetSides = 2;

    /// <summary>The sheets across a poster page.</summary>
    private const int PosterTilesAcross = 2;

    /// <summary>The pages in the source document.</summary>
    private const int Pages = 4;

    /// <summary>The third page, exported first.</summary>
    private const int ThirdPage = 2;

    /// <summary>The number of pages exported.</summary>
    private const int ExportedPages = 2;

    /// <summary>Where the note is added.</summary>
    private static readonly PagePoint NoteAt = new(100, 100);

    /// <summary>Verifies the chosen pages come out in order, with the annotation added before exporting.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExportsChosenPagesWithAnnotations()
    {
        var source = TestPdf.WriteTempFile(Pages);
        var exported = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-export-{Guid.NewGuid():N}.pdf");
        try
        {
            using (var document = new PdfiumEngine().Open(source, null))
            {
                _ = ((IAnnotationEditor)document).AddNote(0, NoteAt, "On the first page", AnnotationColors.Sand);
                await using var stream = File.Create(exported);
                var written = ((IPageExporter)document).ExportPages([ThirdPage, 0], SheetLayout.Default, stream);
                await Assert.That(written).IsTrue();
            }

            using var copy = new PdfiumEngine().Open(exported, null);
            List<PageAnnotation> annotations = [];
            ((IAnnotationEditor)copy).GetAnnotations(1, annotations);

            await Assert.That(copy.PageCount).IsEqualTo(ExportedPages);
            await Assert.That(copy.GetText(0, 0, copy.GetCharacterCount(0))).Contains("Page 3");
            await Assert.That(copy.GetText(1, 0, copy.GetCharacterCount(1))).Contains("Page 1");
            await Assert.That(annotations.Exists(static a => a.Kind == AnnotationKind.Note)).IsTrue();
        }
        finally
        {
            File.Delete(source);
            File.Delete(exported);
        }
    }

    /// <summary>Verifies several pages share a sheet, sideways for two per sheet, and annotations can be left out.</summary>
    /// <param name="perSheet">Pages per sheet.</param>
    /// <param name="sheets">The sheets expected for four pages.</param>
    /// <param name="landscape">Whether the sheet should be sideways.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(2, 2, true)]
    [Arguments(4, 1, false)]
    public async Task LaysPagesOntoSheets(int perSheet, int sheets, bool landscape)
    {
        var source = TestPdf.WriteTempFile(Pages);
        var exported = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-nup-{Guid.NewGuid():N}.pdf");
        try
        {
            using (var document = new PdfiumEngine().Open(source, null))
            {
                _ = ((IAnnotationEditor)document).AddNote(0, NoteAt, "Left out", AnnotationColors.Sand);
                await using var stream = File.Create(exported);
                var written = ((IPageExporter)document).ExportPages([0, 1, ThirdPage, Pages - 1], new(perSheet, PaperSize.A4, false), stream);
                await Assert.That(written).IsTrue();
            }

            using var copy = new PdfiumEngine().Open(exported, null);
            var size = copy.GetPageSizes()[0];
            List<PageAnnotation> annotations = [];
            ((IAnnotationEditor)copy).GetAnnotations(0, annotations);

            await Assert.That(copy.PageCount).IsEqualTo(sheets);
            await Assert.That(size.Width > size.Height).IsEqualTo(landscape);
            await Assert.That(annotations.Count).IsEqualTo(0);
        }
        finally
        {
            File.Delete(source);
            File.Delete(exported);
        }
    }

    /// <summary>A booklet puts two pages side by side on sideways sheets, both sides, padded to a multiple of four.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PrintsABooklet()
    {
        var copy = await ExportAsync([0, 1, ThirdPage], SheetLayout.Default with { Imposition = PrintImposition.Booklet });
        using (copy)
        {
            var size = copy.GetPageSizes()[0];

            await Assert.That(copy.PageCount).IsEqualTo(BookletSheetSides);
            await Assert.That(size.Width > size.Height).IsTrue();
        }
    }

    /// <summary>A poster spreads each page over tiles-squared sheets, each filled with part of the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PrintsAPoster()
    {
        var copy = await ExportAsync([0], SheetLayout.Default with { Imposition = PrintImposition.Poster, PosterTiles = PosterTilesAcross });
        using (copy)
        {
            var size = copy.GetPageSizes()[0];

            await Assert.That(copy.PageCount).IsEqualTo(PosterTilesAcross * PosterTilesAcross);
            await Assert.That(size.Height > size.Width).IsTrue();
            await Assert.That(HasInk(copy, 0)).IsTrue();
        }
    }

    /// <summary>Verifies pages outside the document are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesMissingPages()
    {
        var source = TestPdf.WriteTempFile(1);
        try
        {
            using var document = new PdfiumEngine().Open(source, null);
            await using var stream = new MemoryStream();

            await Assert.That(((IPageExporter)document).ExportPages([Pages], SheetLayout.Default, stream)).IsFalse();
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>Exports pages of a generated document and opens the result.</summary>
    /// <param name="pages">The pages.</param>
    /// <param name="layout">The layout.</param>
    /// <returns>The exported document.</returns>
    private static async Task<IDocument> ExportAsync(int[] pages, SheetLayout layout)
    {
        var source = TestPdf.WriteTempFile(Pages);
        var exported = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-impose-{Guid.NewGuid():N}.pdf");
        try
        {
            using (var document = new PdfiumEngine().Open(source, null))
            {
                await using var stream = File.Create(exported);
                _ = ((IPageExporter)document).ExportPages(pages, layout, stream);
            }

            return new PdfiumEngine().Open(exported, null);
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>Determines whether a page renders anything but white.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns><see langword="true"/> when something is drawn.</returns>
    private static bool HasInk(IDocument document, int page)
    {
        const int bytesPerPixel = 4;
        const byte white = 0xFF;
        var size = document.GetPageSizes()[page];
        var width = (int)size.Width;
        var height = (int)size.Height;
        var pixels = new byte[width * height * bytesPerPixel];
        _ = document.Render(new(page, 1, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, width, height, width * bytesPerPixel));
        return pixels.AsSpan().ContainsAnyExcept(white);
    }
}
