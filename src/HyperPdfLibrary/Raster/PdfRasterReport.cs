// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Raster;

/// <summary>
/// Whether a file is a raster-only (scanned) document and whether it follows the shape of PDF/R (ISO 23504-1). The
/// report judges structure only: every page paints images and no vector or visible text. It does not prove
/// conformance, since the standard text was not available.
/// </summary>
/// <param name="Claim">
/// The file's PDF/R claim, for example "PDF-raster-1.0", read from a <c>%PDF-raster-x.y</c> comment in the last
/// 8 KiB of the file; <see langword="null"/> when there is none. The comment form comes from the PDF Association's
/// description and third-party validator documentation, not from the standard's text, and the claim is not validated.
/// </param>
/// <param name="Pages">One entry per page.</param>
/// <param name="FiltersSeen">The distinct image filters used, in first-seen order.</param>
[DebuggerDisplay("PdfRasterReport: {Pages.Count} pages, raster-only {IsRasterOnly}, claim {Claim}")]
public sealed record PdfRasterReport(string? Claim, IReadOnlyList<PdfRasterPage> Pages, IReadOnlyList<string> FiltersSeen)
{
    /// <summary>Gets a value indicating whether the file has pages and every one is raster-only.</summary>
    public bool IsRasterOnly
    {
        get
        {
            for (var i = 0; i < Pages.Count; i++)
            {
                if (!Pages[i].IsRasterOnly)
                {
                    return false;
                }
            }

            return Pages.Count > 0;
        }
    }

    /// <summary>Gets a value indicating whether every image in the file uses only Flate, CCITT, DCT, JBIG2 or JPX.</summary>
    public bool UsesAllowedFilters
    {
        get
        {
            for (var i = 0; i < Pages.Count; i++)
            {
                if (!Pages[i].UsesAllowedFilters)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Gets the zero based indexes of the pages that are not raster-only.</summary>
    /// <returns>The page indexes, in order.</returns>
    public int[] GetNonRasterPages()
    {
        List<int> found = [];
        for (var i = 0; i < Pages.Count; i++)
        {
            if (!Pages[i].IsRasterOnly)
            {
                found.Add(Pages[i].PageIndex);
            }
        }

        return [.. found];
    }
}
