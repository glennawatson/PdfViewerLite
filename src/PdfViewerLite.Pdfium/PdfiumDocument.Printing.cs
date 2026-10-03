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

        var imported = ImportPages(copy, pages);
        if (imported == 0)
        {
            return false;
        }

        if (!layout.IncludeAnnotations)
        {
            RemoveMarkup(copy, imported);
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
}
