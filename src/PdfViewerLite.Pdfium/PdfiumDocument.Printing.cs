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

        if (layout.PagesPerSheet <= 1)
        {
            return WriteDocument(copy, destination, SaveFull);
        }

        SheetGrid.GetGrid(layout.PagesPerSheet, out var columns, out var rows, out var landscape);
        SheetGrid.GetSheetSize(layout.Paper, landscape, out var width, out var height);
        using var sheets = NativeMethods.FPDF_ImportNPagesToOne(copy, width, height, (nuint)columns, (nuint)rows);
        return !sheets.IsInvalid && WriteDocument(sheets, destination, SaveFull);
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
