// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>Where each character of a page is, for working out reading order.</summary>
public sealed partial class PdfiumDocument : ITextLayoutSource
{
    /// <summary>The font weight from which text counts as bold.</summary>
    private const int BoldWeight = 600;

    /// <summary>A typical font's ascent-to-descent height relative to its size.</summary>
    private const float LooseBoxToSize = 1.15F;

    /// <summary>How much larger the drawn size must be before the reported font size is treated as scaled.</summary>
    private const float ScaledTextRatio = 1.5F;

    /// <inheritdoc/>
    public void GetCharacters(int pageIndex, List<PageCharacter> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var scope = PdfiumLibrary.EnterScope();
        var page = IsDisposed ? null : GetPage(pageIndex);
        var textPage = page?.TextPage;
        if (page is null || textPage is null)
        {
            return;
        }

        var count = NativeMethods.FPDFText_CountChars(textPage);
        _ = output.EnsureCapacity(output.Count + count);
        for (var i = 0; i < count; i++)
        {
            var value = (char)NativeMethods.FPDFText_GetUnicode(textPage, i);
            var generated = NativeMethods.FPDFText_IsGenerated(textPage, i) == 1;
            var bounds = generated ? default : Bounds(page, textPage, i);
            var size = EffectiveSize(textPage, i, generated);
            var bold = NativeMethods.FPDFText_GetFontWeight(textPage, i) >= BoldWeight;
            output.Add(new(value, bounds, size, bold, generated));
        }
    }

    /// <summary>
    /// Gets a character's box from its advance and the font's ascent and descent, so narrow letters such as I and 1
    /// keep their side bearings and do not look like the gap between words; the ink box is the fallback.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="textPage">The text page.</param>
    /// <param name="index">The character.</param>
    /// <returns>The box in viewer space, or empty when PDFium has none.</returns>
    private static PageRect Bounds(PdfiumPage page, PdfiumTextPageHandle textPage, int index)
    {
        if (NativeMethods.FPDFText_GetLooseCharBox(textPage, index, out var loose) != 0 && loose.Right > loose.Left)
        {
            return page.ToViewer(loose.Left, loose.Top, loose.Right, loose.Bottom);
        }

        return NativeMethods.FPDFText_GetCharBox(textPage, index, out var left, out var right, out var bottom, out var top) != 0
            ? page.ToViewer(left, top, right, bottom)
            : default;
    }

    /// <summary>
    /// Gets the size a character is drawn at. PDFium reports the font size before the text matrix scales it, so
    /// text set at size 1 and scaled up (as Quartz and some Word exports do) would look tiny; the height of the
    /// character's ascent-to-descent box is used then instead.
    /// </summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="index">The character.</param>
    /// <param name="generated">Whether PDFium made the character up, such as a space or line break.</param>
    /// <returns>The size in points.</returns>
    private static float EffectiveSize(PdfiumTextPageHandle textPage, int index, bool generated)
    {
        var size = (float)NativeMethods.FPDFText_GetFontSize(textPage, index);
        if (generated || NativeMethods.FPDFText_GetLooseCharBox(textPage, index, out var box) == 0)
        {
            return size;
        }

        var drawn = (box.Top - box.Bottom) / LooseBoxToSize;
        return drawn > size * ScaledTextRatio ? drawn : size;
    }
}
