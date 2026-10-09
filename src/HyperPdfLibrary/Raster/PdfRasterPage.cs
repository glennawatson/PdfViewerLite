// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Raster;

/// <summary>What one page paints, judged by structure alone.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="IsReadable">Whether the page's content could be read to the end.</param>
/// <param name="Images">The images the page paints, in painting order.</param>
/// <param name="HasVectorContent">Whether the page fills, strokes or shades a path or shading.</param>
/// <param name="HasVisibleText">Whether the page shows text in a render mode other than 3 (invisible).</param>
/// <param name="HasOcrText">Whether the page shows invisible (render mode 3) text, the usual OCR layer.</param>
/// <param name="OcrCharCount">The characters the text extractor finds on the page when it has an invisible text layer; otherwise 0.</param>
[DebuggerDisplay("PdfRasterPage: page {PageIndex}, raster-only {IsRasterOnly}, {Images.Count} images")]
public sealed record PdfRasterPage(
    int PageIndex,
    bool IsReadable,
    IReadOnlyList<PdfRasterImage> Images,
    bool HasVectorContent,
    bool HasVisibleText,
    bool HasOcrText,
    int OcrCharCount)
{
    /// <summary>
    /// Gets a value indicating whether the page paints at least one image and nothing else except invisible text: no
    /// path painting, shading or visible text.
    /// </summary>
    public bool IsRasterOnly => IsReadable && Images.Count > 0 && !HasVectorContent && !HasVisibleText;

    /// <summary>Gets a value indicating whether every image on the page uses only Flate, CCITT, DCT, JBIG2 or JPX.</summary>
    public bool UsesAllowedFilters
    {
        get
        {
            for (var i = 0; i < Images.Count; i++)
            {
                if (!Images[i].UsesAllowedFilters)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
