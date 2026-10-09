// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Raster;

/// <summary>Builds a document's <see cref="PdfRasterReport"/> by scanning every page.</summary>
internal static class RasterReportBuilder
{
    /// <summary>Builds the report.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellation">The cancellation token, checked once per page.</param>
    /// <returns>The report.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfRasterReport Build(PdfDocument document, CancellationToken cancellation) => Build(document, true, cancellation);

    /// <summary>Builds the report.</summary>
    /// <param name="document">The document.</param>
    /// <param name="countText">Whether to extract the text of pages with an invisible text layer to count its characters.</param>
    /// <param name="cancellation">The cancellation token, checked once per page.</param>
    /// <returns>The report.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static PdfRasterReport Build(PdfDocument document, bool countText, CancellationToken cancellation)
    {
        var scanner = new RasterPageScanner(document);
        var pages = new List<PdfRasterPage>(document.PageCount);
        List<string> filters = [];
        for (var i = 0; i < document.PageCount; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var page = ScanPage(document, scanner, i, countText);
            pages.Add(page);
            AddFilters(page, filters);
        }

        return new(RasterClaimReader.Read(document.Objects.Source), pages, filters);
    }

    /// <summary>Scans one page, keeping what was read when its content is damaged.</summary>
    /// <param name="document">The document.</param>
    /// <param name="scanner">The reusable scanner.</param>
    /// <param name="index">The page index.</param>
    /// <param name="countText">Whether to count the characters of an invisible text layer.</param>
    /// <returns>The page's entry.</returns>
    private static PdfRasterPage ScanPage(PdfDocument document, RasterPageScanner scanner, int index, bool countText)
    {
        var readable = true;
        try
        {
            scanner.Scan(PdfDocumentPages.GetPage(document, index));
        }
        catch (Exception ex) when (ex is InvalidDataException or PdfException or ArgumentException or InvalidOperationException
            or IndexOutOfRangeException or NotSupportedException or FormatException or OverflowException)
        {
            readable = false;
        }

        var characters = countText && scanner.HasOcrText ? CountCharacters(document, index) : 0;
        return new(index, readable, [.. scanner.Images], scanner.HasVectorContent, scanner.HasVisibleText, scanner.HasOcrText, characters);
    }

    /// <summary>Counts the characters the text extractor finds on a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The page index.</param>
    /// <returns>The count; 0 when the page's text cannot be read.</returns>
    private static int CountCharacters(PdfDocument document, int index)
    {
        try
        {
            return PdfDocumentText.GetTextPage(document, index).CharCount;
        }
        catch (Exception ex) when (ex is InvalidDataException or PdfException or ArgumentException or InvalidOperationException
            or IndexOutOfRangeException or NotSupportedException or FormatException or OverflowException)
        {
            return 0;
        }
    }

    /// <summary>Adds a page's filters that have not been seen yet.</summary>
    /// <param name="page">The page.</param>
    /// <param name="filters">The distinct filters so far.</param>
    private static void AddFilters(PdfRasterPage page, List<string> filters)
    {
        foreach (var image in page.Images)
        {
            foreach (var filter in image.Filters)
            {
                if (!filters.Contains(filter))
                {
                    filters.Add(filter);
                }
            }
        }
    }
}
