// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.TextLayer;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Adds invisible text layers to image-only pages from the words a text recognition hook returns, through
/// <see cref="PdfTextLayer"/>, so search, selection and copy work on scans. Text is written in Helvetica; characters it
/// cannot show are left out.
/// </summary>
internal static class OcrLayerPass
{
    /// <summary>Adds the layers.</summary>
    /// <param name="document">The working copy.</param>
    /// <param name="ocr">The hook: page index to words, or <see langword="null"/>.</param>
    /// <param name="report">Receives the pages changed.</param>
    /// <param name="cancellationToken">Stops the pass between pages.</param>
    internal static void Run(PdfDocument document, Func<int, IReadOnlyList<PdfOcrWord>?> ocr, OptimizeReportBuilder report, CancellationToken cancellationToken)
    {
        IPdfTextLayerFont[] fonts = [new PdfStandardTextLayerFont(document.Objects, AppearanceFont.Helvetica)];
        for (var i = 0; i < document.PageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!document.GetMarkedContent(i).IsImageOnly || ocr(i) is not { Count: > 0 } words)
            {
                continue;
            }

            var layer = new PdfTextLayerWord[words.Count];
            for (var w = 0; w < words.Count; w++)
            {
                var (text, bounds, _) = words[w];
                layer[w] = new(text, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom, 0);
            }

            var written = PdfTextLayer.Append(document.Objects, document.GetPage(i), layer, fonts);
            if (written == 0)
            {
                continue;
            }

            document.RefreshAfterOptimizerEdit();
            var note = string.Create(CultureInfo.InvariantCulture, $"Added an invisible text layer of {written} words to page {i + 1}.");
            report.Noted(PdfOptimizeCategory.TextLayer, document.GetPage(i).Id.Number, note);
        }
    }
}
