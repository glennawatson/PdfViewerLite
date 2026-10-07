// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Printing;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>Exporting chosen pages, for printing part of a document.</summary>
public sealed partial class PdfiumDocument : IPageExporter
{
    /// <summary>PDFium's form widget subtype, kept when annotations are left out so filled fields still print.</summary>
    private const int WidgetSubtype = 20;

    /// <summary>The pages on both sides of a booklet sheet.</summary>
    private const int BookletPagesPerSheet = 4;

    /// <summary>The pages side by side on a booklet sheet.</summary>
    private const nuint BookletColumns = 2;

    /// <summary>The overlap between poster tiles, as a share of a tile, so neighbouring sheets can be joined.</summary>
    private const float PosterOverlap = 0.04F;

    /// <summary>Flattens only annotations that are marked for printing.</summary>
    private const int FlattenForPrint = 1;

    /// <summary>A quarter-inch margin keeps content clear of common printer edges.</summary>
    private const float PrintMargin = 18F;

    /// <summary>The number of margins on each axis.</summary>
    private const int MarginsPerAxis = 2;

    /// <inheritdoc/>
    public bool ExportPages(ReadOnlySpan<int> pages, in SheetLayout layout, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        using var scope = PdfiumLibrary.EnterScope();
        if (IsDisposed || pages.IsEmpty)
        {
            return false;
        }

        foreach (var page in pages)
        {
            if ((uint)page >= (uint)PageCount)
            {
                return false;
            }
        }

        using var copy = NativeMethods.FPDF_CreateNewDocument();
        if (copy.IsInvalid)
        {
            return false;
        }

        var imported = layout.Imposition switch
        {
            PrintImposition.Booklet => ImportBooklet(copy, pages),
            PrintImposition.Poster => ImportPosterTiles(copy, pages, Math.Max(1, layout.PosterTiles)),
            _ => ImportPages(copy, pages),
        };
        if (imported == 0)
        {
            return false;
        }

        if (!layout.IncludeAnnotations)
        {
            RemoveMarkup(copy, imported);
        }

        return WriteSheets(copy, layout, destination);
    }

    /// <summary>Lays the copied pages onto sheets and writes them. Callers hold the PDFium lock.</summary>
    /// <param name="copy">The copied pages: in order, in booklet order, or as poster tiles.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="destination">The stream.</param>
    /// <returns><see langword="true"/> when written.</returns>
    private static bool WriteSheets(PdfiumDocumentHandle copy, in SheetLayout layout, Stream destination)
    {
        if (!PrepareSheets(copy, layout))
        {
            return false;
        }

        if (layout.Imposition == PrintImposition.Booklet)
        {
            // Two pages side by side on a sideways sheet.
            SheetGrid.GetSheetSize(layout.Paper, true, out var sheetWidth, out var sheetHeight);
            using var booklet = NativeMethods.FPDF_ImportNPagesToOne(copy, sheetWidth, sheetHeight, BookletColumns, 1);
            return !booklet.IsInvalid && WriteDocument(booklet, destination, SaveFull);
        }

        if (layout.Imposition == PrintImposition.Poster)
        {
            // Each tile fills a sheet turned to match it.
            var tileLandscape = NativeMethods.FPDF_GetPageSizeByIndexF(copy, 0, out var tile) != 0 && tile.Width > tile.Height;
            SheetGrid.GetSheetSize(layout.Paper, tileLandscape, out var posterWidth, out var posterHeight);
            using var poster = NativeMethods.FPDF_ImportNPagesToOne(copy, posterWidth, posterHeight, 1, 1);
            return !poster.IsInvalid && WriteDocument(poster, destination, SaveFull);
        }

        return layout.PagesPerSheet <= 1 && !layout.FitToPaper
            ? WriteDocument(copy, destination, SaveFull)
            : WritePageGrid(copy, layout, destination);
    }

    /// <summary>Flattens fields and printed annotations before resizing or arranging pages.</summary>
    /// <param name="copy">The print copy.</param>
    /// <param name="layout">The sheet layout.</param>
    /// <returns>Whether the copy is ready for layout.</returns>
    private static bool PrepareSheets(PdfiumDocumentHandle copy, in SheetLayout layout)
    {
        var needsFlattening = layout.FitToPaper || layout.PagesPerSheet > 1 || layout.Imposition != PrintImposition.Pages;
        return !needsFlattening || FlattenPages(copy);
    }

    /// <summary>Writes pages fitted to a grid on the chosen paper.</summary>
    /// <param name="copy">The copied pages.</param>
    /// <param name="layout">The sheet layout.</param>
    /// <param name="destination">The stream.</param>
    /// <returns>Whether the sheets were written.</returns>
    private static bool WritePageGrid(PdfiumDocumentHandle copy, in SheetLayout layout, Stream destination)
    {
        SheetGrid.GetGrid(layout.PagesPerSheet, out var columns, out var rows, out var landscape);
        SheetGrid.GetSheetSize(layout.Paper, landscape, out var width, out var height);
        if (layout.FitToPaper && layout.PagesPerSheet <= 1)
        {
            return FitPages(copy, width, height, layout) && WriteDocument(copy, destination, SaveFull);
        }

        using var sheets = NativeMethods.FPDF_ImportNPagesToOne(copy, width, height, (nuint)columns, (nuint)rows);
        return !sheets.IsInvalid && WriteDocument(sheets, destination, SaveFull);
    }

    /// <summary>Fits each page inside the paper, turning the paper sideways for wide pages.</summary>
    /// <param name="document">The flattened print copy.</param>
    /// <param name="width">Paper width.</param>
    /// <param name="height">Paper height.</param>
    /// <param name="layout">The sheet layout, which says how pages are sized.</param>
    /// <returns>Whether all pages fit.</returns>
    private static bool FitPages(PdfiumDocumentHandle document, float width, float height, in SheetLayout layout)
    {
        var count = NativeMethods.FPDF_GetPageCount(document);
        for (var index = 0; index < count; index++)
        {
            using var page = NativeMethods.FPDF_LoadPage(document, index);
            if (page.IsInvalid || !FitPage(page, width, height, layout.Scaling, layout.ScalePercent))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Sizes and centres a page's visible content on the paper, cutting off whatever falls outside it.</summary>
    /// <param name="page">The flattened page.</param>
    /// <param name="width">Paper width.</param>
    /// <param name="height">Paper height.</param>
    /// <param name="scaling">How the page is sized.</param>
    /// <param name="percent">The custom percentage.</param>
    /// <returns>Whether the page was transformed.</returns>
    private static bool FitPage(PdfiumPageHandle page, float width, float height, PrintScaling scaling, int percent)
    {
        if (NativeMethods.FPDFPage_GetMediaBox(page, out var left, out var bottom, out var right, out var top) == 0)
        {
            return false;
        }

        if (NativeMethods.FPDFPage_GetCropBox(page, out var cropLeft, out var cropBottom, out var cropRight, out var cropTop) != 0)
        {
            left = Math.Max(left, cropLeft);
            bottom = Math.Max(bottom, cropBottom);
            right = Math.Min(right, cropRight);
            top = Math.Min(top, cropTop);
        }

        if (right <= left || top <= bottom)
        {
            return false;
        }

        // Wide content goes on sideways paper, as printers turn it, so it is not shrunk onto an upright sheet.
        var turned = (NativeMethods.FPDFPage_GetRotation(page) & 1) != 0;
        var wide = turned ? top - bottom > right - left : right - left > top - bottom;
        if (wide != turned)
        {
            (width, height) = (height, width);
        }

        const float Margins = MarginsPerAxis * PrintMargin;
        var scale = PrintScale.For(scaling, percent, right - left, top - bottom, width - Margins, height - Margins);
        var x = ((width - ((right - left) * scale)) / MarginsPerAxis) - (left * scale);
        var y = ((height - ((top - bottom) * scale)) / MarginsPerAxis) - (bottom * scale);
        var clip = new FsRectF(
            Math.Max(0, (left * scale) + x),
            Math.Min(height, (top * scale) + y),
            Math.Min(width, (right * scale) + x),
            Math.Max(0, (bottom * scale) + y));
        if (NativeMethods.FPDFPage_TransFormWithClip(page, new(scale, 0, 0, scale, x, y), clip) == 0)
        {
            return false;
        }

        NativeMethods.FPDFPage_SetMediaBox(page, 0, 0, width, height);
        NativeMethods.FPDFPage_SetCropBox(page, 0, 0, width, height);
        return true;
    }

    /// <summary>Preserves printed fields and annotations before arranging pages on paper.</summary>
    /// <param name="document">The print copy.</param>
    /// <returns>Whether every page was prepared.</returns>
    private static bool FlattenPages(PdfiumDocumentHandle document)
    {
        var count = NativeMethods.FPDF_GetPageCount(document);
        for (var index = 0; index < count; index++)
        {
            using var page = NativeMethods.FPDF_LoadPage(document, index);
            if (page.IsInvalid || NativeMethods.FPDFPage_Flatten(page, FlattenForPrint) == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Removes every annotation except form fields from a document's pages. Callers hold the PDFium lock.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageCount">Its page count.</param>
    private static void RemoveMarkup(PdfiumDocumentHandle document, int pageCount)
    {
        for (var index = 0; index < pageCount; index++)
        {
            using var page = NativeMethods.FPDF_LoadPage(document, index);
            if (page.IsInvalid)
            {
                continue;
            }

            for (var i = NativeMethods.FPDFPage_GetAnnotCount(page) - 1; i >= 0; i--)
            {
                var annotation = NativeMethods.FPDFPage_GetAnnot(page, i);
                var subtype = annotation == 0 ? WidgetSubtype : NativeMethods.FPDFAnnot_GetSubtype(annotation);
                if (annotation != 0)
                {
                    NativeMethods.FPDFPage_CloseAnnot(annotation);
                }

                if (subtype != WidgetSubtype)
                {
                    _ = NativeMethods.FPDFPage_RemoveAnnot(page, i);
                }
            }
        }
    }

    /// <summary>Narrows a page's media and crop boxes to one poster tile, with an overlap. Callers hold the PDFium lock.</summary>
    /// <param name="imported">The page copy.</param>
    /// <param name="tile">The tile, left to right then top to bottom.</param>
    /// <param name="tiles">The tiles across and down.</param>
    private static void NarrowToTile(PdfiumPageHandle imported, int tile, int tiles)
    {
        if (NativeMethods.FPDFPage_GetMediaBox(imported, out var left, out var bottom, out var right, out var top) == 0)
        {
            return;
        }

        var width = (right - left) / tiles;
        var height = (top - bottom) / tiles;
        var column = tile % tiles;
        var row = tile / tiles;
        var tileLeft = Math.Max(left, left + (column * width) - (width * PosterOverlap));
        var tileRight = Math.Min(right, left + ((column + 1) * width) + (width * PosterOverlap));
        var tileTop = Math.Min(top, top - (row * height) + (height * PosterOverlap));
        var tileBottom = Math.Max(bottom, top - ((row + 1) * height) - (height * PosterOverlap));
        NativeMethods.FPDFPage_SetMediaBox(imported, tileLeft, tileBottom, tileRight, tileTop);
        NativeMethods.FPDFPage_SetCropBox(imported, tileLeft, tileBottom, tileRight, tileTop);
    }

    /// <summary>
    /// Copies pages into a new document. When the batch fails, as it does when a damaged file has pages PDFium cannot
    /// load, the pages are copied one at a time and the broken ones left out, so the rest still print.
    /// Callers hold the PDFium lock.
    /// </summary>
    /// <param name="copy">The new document.</param>
    /// <param name="pages">The pages, zero-based.</param>
    /// <returns>The number of pages copied.</returns>
    private unsafe int ImportPages(PdfiumDocumentHandle copy, ReadOnlySpan<int> pages)
    {
        fixed (int* indices = pages)
        {
            if (NativeMethods.FPDF_ImportPagesByIndex(copy, _handle, indices, new((uint)pages.Length), 0) != 0)
            {
                return pages.Length;
            }

            var imported = 0;
            for (var i = 0; i < pages.Length; i++)
            {
                imported += NativeMethods.FPDF_ImportPagesByIndex(copy, _handle, indices + i, new(1U), imported) != 0 ? 1 : 0;
            }

            return imported;
        }
    }

    /// <summary>
    /// Copies pages into a new document in booklet order, adding blank pages the size of the first so the count is a
    /// multiple of four. Callers hold the PDFium lock.
    /// </summary>
    /// <param name="copy">The new document.</param>
    /// <param name="pages">The pages, zero-based, in reading order.</param>
    /// <returns>The number of pages in the new document.</returns>
    private unsafe int ImportBooklet(PdfiumDocumentHandle copy, ReadOnlySpan<int> pages)
    {
        var order = new List<int>(Booklet.SheetCount(pages.Length) * BookletPagesPerSheet);
        Booklet.Arrange(pages.Length, order);
        _ = NativeMethods.FPDF_GetPageSizeByIndexF(_handle, pages[0], out var size);
        var count = 0;
        fixed (int* indices = pages)
        {
            foreach (var position in order)
            {
                if (position < 0)
                {
                    using var blank = NativeMethods.FPDFPage_New(copy, count, size.Width, size.Height);
                    count += blank.IsInvalid ? 0 : 1;
                    continue;
                }

                count += NativeMethods.FPDF_ImportPagesByIndex(copy, _handle, indices + position, new(1U), count) != 0 ? 1 : 0;
            }
        }

        return count;
    }

    /// <summary>
    /// Copies each page once per poster tile and narrows each copy's boxes to its tile, a little larger than an even
    /// share so neighbouring sheets overlap. Tiles run left to right, top to bottom. Callers hold the PDFium lock.
    /// </summary>
    /// <param name="copy">The new document.</param>
    /// <param name="pages">The pages, zero-based.</param>
    /// <param name="tiles">The tiles across, and down, each page.</param>
    /// <returns>The number of tiles in the new document.</returns>
    private unsafe int ImportPosterTiles(PdfiumDocumentHandle copy, ReadOnlySpan<int> pages, int tiles)
    {
        var count = 0;
        fixed (int* indices = pages)
        {
            for (var i = 0; i < pages.Length * tiles * tiles; i++)
            {
                if (NativeMethods.FPDF_ImportPagesByIndex(copy, _handle, indices + (i / (tiles * tiles)), new(1U), count) == 0)
                {
                    continue;
                }

                using var imported = NativeMethods.FPDF_LoadPage(copy, count);
                count++;
                if (!imported.IsInvalid)
                {
                    NarrowToTile(imported, i % (tiles * tiles), tiles);
                }
            }
        }

        return count;
    }
}
