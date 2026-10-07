// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
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

    /// <summary>The characters in the first page's heading, "Page 1".</summary>
    private const int HeadingLength = 6;

    /// <summary>Half the true size, as a percentage.</summary>
    private const int HalfScale = 50;

    /// <summary>The share by which printed sizes may differ from the expected scale.</summary>
    private const double ScaleTolerance = 0.05;

    /// <summary>The index of the test document's wide page.</summary>
    private const int WidePage = 1;

    /// <summary>The third page, exported first.</summary>
    private const int ThirdPage = 2;

    /// <summary>The number of pages exported.</summary>
    private const int ExportedPages = 2;

    /// <summary>The filled value that must survive printing.</summary>
    private const string PrintedField = "Printed field value";

    /// <summary>The visible signature used to check print copies.</summary>
    private const string PrintedSignature = "Sample Signer";

    /// <summary>The signature text size in PDF points.</summary>
    private const float SignatureSize = 24;

    /// <summary>The pages arranged side by side in a print grid.</summary>
    private const int GridPages = 2;

    /// <summary>The width of drawn ink in points.</summary>
    private const float InkWidth = 2;

    /// <summary>Where the note is added.</summary>
    private static readonly PagePoint NoteAt = new(100, 100);

    /// <summary>A drawn signature on empty paper below the page's text.</summary>
    private static readonly PagePoint[] DrawnSignature = [new(100, 500), new(160, 440), new(220, 500), new(280, 440)];

    /// <summary>Print layouts keep filled fields in the saved page content.</summary>
    /// <param name="imposition">The print layout.</param>
    /// <param name="pagesPerSheet">The pages on each sheet.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PrintImposition.Pages, 2)]
    [Arguments(PrintImposition.Booklet, 1)]
    [Arguments(PrintImposition.Poster, 1)]
    public async Task PrintLayoutsKeepFilledFields(PrintImposition imposition, int pagesPerSheet)
    {
        var source = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-form-layout-source-{Guid.NewGuid():N}.pdf");
        var exported = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-form-layout-print-{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllBytesAsync(source, TestPdf.CreateForm());
            using (var document = new PdfiumEngine().Open(source, null))
            {
                var filler = (IFormFiller)document;
                List<FormField> fields = [];
                filler.GetFields(0, fields);
                await Assert.That(filler.SetText(0, fields[0].Index, PrintedField)).IsTrue();
                await using var stream = File.Create(exported);
                var layout = new SheetLayout(pagesPerSheet, PaperSize.A4, false) { Imposition = imposition, PosterTiles = 1 };
                await Assert.That(((IPageExporter)document).ExportPages([0], layout, stream)).IsTrue();
                fields.Clear();
                filler.GetFields(0, fields);
                await Assert.That(fields[0].Value).IsEqualTo(PrintedField);
            }

            using var copy = new PdfiumEngine().Open(exported, null);
            var text = string.Concat(Enumerable.Range(0, copy.PageCount).Select(page => copy.GetText(page, 0, copy.GetCharacterCount(page))));
            await Assert.That(text).Contains(PrintedField);
        }
        finally
        {
            File.Delete(source);
            File.Delete(exported);
        }
    }

    /// <summary>Print layouts keep visible signatures only when annotations are included.</summary>
    /// <param name="imposition">The print layout.</param>
    /// <param name="includeAnnotations">Whether the signature should print.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PrintImposition.Pages, true)]
    [Arguments(PrintImposition.Pages, false)]
    [Arguments(PrintImposition.Booklet, true)]
    [Arguments(PrintImposition.Booklet, false)]
    [Arguments(PrintImposition.Poster, true)]
    [Arguments(PrintImposition.Poster, false)]
    public async Task PrintLayoutsRespectSignatureChoice(PrintImposition imposition, bool includeAnnotations)
    {
        using var source = new TestDocument(1);
        var editor = (IAnnotationEditor)source.Document;
        await Assert.That(editor.AddText(0, NoteAt, PrintedSignature, SignatureSize, AnnotationColors.Ink, AnnotationKind.Signature)).IsGreaterThanOrEqualTo(0);
        var exported = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-signature-layout-{Guid.NewGuid():N}.pdf");
        try
        {
            await using (var stream = File.Create(exported))
            {
                var layout = new SheetLayout(GridPages, PaperSize.A4, includeAnnotations) { Imposition = imposition, PosterTiles = 1 };
                await Assert.That(((IPageExporter)source.Document).ExportPages([0], layout, stream)).IsTrue();
            }

            using var copy = new PdfiumEngine().Open(exported, null);
            var text = string.Concat(Enumerable.Range(0, copy.PageCount).Select(page => copy.GetText(page, 0, copy.GetCharacterCount(page))));
            await Assert.That(text.Contains(PrintedSignature, StringComparison.Ordinal)).IsEqualTo(includeAnnotations);
            List<PageAnnotation> original = [];
            editor.GetAnnotations(0, original);
            await Assert.That(original.Count).IsEqualTo(1);
            await Assert.That(original[0].Kind).IsEqualTo(AnnotationKind.Signature);
        }
        finally
        {
            File.Delete(exported);
        }
    }

    /// <summary>
    /// A drawn signature, even after its colour is changed, is still on the sheet when the print copy is flattened onto
    /// a grid, because the ink carries its own appearance.
    /// </summary>
    /// <param name="recolour">Whether the ink's colour is changed before printing.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PrintLayoutsKeepDrawnSignatures(bool recolour)
    {
        using var signed = new TestDocument(1);
        using var blank = new TestDocument(1);
        var editor = (IAnnotationEditor)signed.Document;
        var index = editor.AddInk(0, DrawnSignature, [DrawnSignature.Length], AnnotationColors.Ink, InkWidth, AnnotationKind.Signature);
        var recoloured = !recolour || editor.SetColor(0, index, AnnotationColors.Clay);

        var withInk = await PrintedInkAsync(signed.Document);
        var without = await PrintedInkAsync(blank.Document);

        await Assert.That(index).IsGreaterThanOrEqualTo(0);
        await Assert.That(recoloured).IsTrue();
        await Assert.That(withInk).IsGreaterThan(without);
    }

    /// <summary>Fitting a filled form to paper keeps the field value in the printed page content.</summary>
    /// <param name="paper">The chosen paper.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PaperSize.A4)]
    [Arguments(PaperSize.Letter)]
    public async Task FitsFilledFormsToPaper(PaperSize paper)
    {
        var source = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-form-source-{Guid.NewGuid():N}.pdf");
        var exported = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-form-print-{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllBytesAsync(source, TestPdf.CreateForm());
            using (var document = new PdfiumEngine().Open(source, null))
            {
                var filler = (IFormFiller)document;
                List<FormField> fields = [];
                filler.GetFields(0, fields);
                _ = filler.SetText(0, fields[0].Index, PrintedField);
                await using var stream = File.Create(exported);
                await Assert.That(((IPageExporter)document).ExportPages([0], new(1, paper, true) { FitToPaper = true }, stream)).IsTrue();
            }

            using var copy = new PdfiumEngine().Open(exported, null);
            SheetGrid.GetSheetSize(paper, false, out var width, out var height);
            await Assert.That(copy.GetPageSizes()[0].Width).IsEqualTo(width);
            await Assert.That(copy.GetPageSizes()[0].Height).IsEqualTo(height);
            await Assert.That(copy.GetText(0, 0, copy.GetCharacterCount(0))).Contains(PrintedField);
        }
        finally
        {
            File.Delete(source);
            File.Delete(exported);
        }
    }

    /// <summary>Fitting to paper puts upright pages on upright paper and wide pages on sideways paper.</summary>
    /// <param name="paper">The chosen paper.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PaperSize.A4)]
    [Arguments(PaperSize.Letter)]
    public async Task FitsEachPageToPaperOfItsOrientation(PaperSize paper)
    {
        var source = TestPdf.WriteTempFile(Pages);
        var exported = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-fit-{Guid.NewGuid():N}.pdf");
        try
        {
            using (var document = new PdfiumEngine().Open(source, null))
            {
                await using var stream = File.Create(exported);
                await Assert.That(((IPageExporter)document).ExportPages([0, WidePage], new(1, paper, true) { FitToPaper = true }, stream)).IsTrue();
            }

            using var copy = new PdfiumEngine().Open(exported, null);
            SheetGrid.GetSheetSize(paper, false, out var width, out var height);
            var sizes = copy.GetPageSizes();
            PageSize upright = new(width, height);
            await Assert.That(sizes[0]).IsEqualTo(upright);
            await Assert.That(sizes[1]).IsEqualTo(upright.Rotate(PageRotation.Rotate90));
            await Assert.That(copy.GetText(1, 0, copy.GetCharacterCount(1))).Contains("Page 2");
        }
        finally
        {
            File.Delete(source);
            File.Delete(exported);
        }
    }

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

    /// <summary>Each scaling choice prints the page at the size it promises, on the chosen paper.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScalesPagesAsChosen()
    {
        var actual = await PrintedTextWidthAsync(PaperSize.A3, PrintScaling.ActualSize, PrintScale.TrueSize);
        var half = await PrintedTextWidthAsync(PaperSize.A3, PrintScaling.Custom, HalfScale);
        var fitLarge = await PrintedTextWidthAsync(PaperSize.A3, PrintScaling.FitToPaper, PrintScale.TrueSize);
        var shrinkLarge = await PrintedTextWidthAsync(PaperSize.A3, PrintScaling.ShrinkOversized, PrintScale.TrueSize);
        var shrinkSmall = await PrintedTextWidthAsync(PaperSize.A5, PrintScaling.ShrinkOversized, PrintScale.TrueSize);

        await Assert.That(half / actual).IsEqualTo(HalfScale / (double)PrintScale.TrueSize).Within(ScaleTolerance);
        await Assert.That(fitLarge).IsGreaterThan(actual * (1 + ScaleTolerance));
        await Assert.That(shrinkLarge).IsEqualTo(actual).Within(actual * ScaleTolerance);
        await Assert.That(shrinkSmall).IsLessThan(actual * (1 - ScaleTolerance));
    }

    /// <summary>Exports the first page on a paper with a scaling choice and measures its heading's printed width.</summary>
    /// <param name="paper">The paper.</param>
    /// <param name="scaling">The scaling choice.</param>
    /// <param name="percent">The custom percentage.</param>
    /// <returns>The heading's width in points.</returns>
    private static async Task<double> PrintedTextWidthAsync(PaperSize paper, PrintScaling scaling, int percent)
    {
        using var copy = await ExportAsync([0], new(1, paper, true) { FitToPaper = true, Scaling = scaling, ScalePercent = percent });
        SheetGrid.GetSheetSize(paper, false, out var width, out var height);
        PageSize sheet = new(width, height);
        await Assert.That(copy.GetPageSizes()[0]).IsEqualTo(sheet);
        List<PageRect> bounds = [];
        copy.GetTextBounds(0, 0, HeadingLength, bounds);
        return bounds.Sum(static rect => (double)rect.Width);
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

    /// <summary>Prints the first page two to a sheet, which flattens it, and counts the sheet's inked pixels.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The pixels that are not white paper.</returns>
    private static async Task<int> PrintedInkAsync(IDocument document)
    {
        const int bytesPerPixel = 4;
        const byte white = 0xFF;
        var exported = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-ink-print-{Guid.NewGuid():N}.pdf");
        try
        {
            await using (var stream = File.Create(exported))
            {
                _ = ((IPageExporter)document).ExportPages([0], new(GridPages, PaperSize.A4, true), stream);
            }

            using var copy = new PdfiumEngine().Open(exported, null);
            var size = copy.GetPageSizes()[0];
            var width = (int)size.Width;
            var height = (int)size.Height;
            var pixels = new byte[width * height * bytesPerPixel];
            _ = copy.Render(new(0, 1, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, width, height, width * bytesPerPixel));
            var inked = 0;
            for (var offset = 0; offset < pixels.Length; offset += bytesPerPixel)
            {
                if (pixels.AsSpan(offset, bytesPerPixel - 1).ContainsAnyExcept(white))
                {
                    inked++;
                }
            }

            return inked;
        }
        finally
        {
            File.Delete(exported);
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
