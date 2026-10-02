// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>Where each character of a page is, for working out reading order.</summary>
public sealed partial class PdfiumDocument : ITextLayoutSource
{
    /// <summary>The font weight from which text counts as bold.</summary>
    private const int BoldWeight = 600;

    /// <summary>The font descriptor flag that asks for bold glyphs to be drawn heavier (bit 19).</summary>
    private const int ForceBoldFlag = 1 << 18;

    /// <summary>The longest base font name read.</summary>
    private const int FontNameBytes = 128;

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
        var fonts = new Dictionary<nint, bool>();
        for (var i = 0; i < count; i++)
        {
            var value = (char)NativeMethods.FPDFText_GetUnicode(textPage, i);
            var generated = NativeMethods.FPDFText_IsGenerated(textPage, i) == 1;
            var bounds = generated ? default : Bounds(page, textPage, i);
            var size = EffectiveSize(textPage, i, generated);
            var bold = !generated && IsBold(textPage, i, fonts);
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

    /// <summary>
    /// Determines whether a character is bold: by its weight, or failing that by its font's descriptor weight,
    /// ForceBold flag or name (many fonts give no weight, but are called Helvetica-Bold or MinionPro-Semibold).
    /// </summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="index">The character.</param>
    /// <param name="fonts">The answer for each font already seen on the page.</param>
    /// <returns><see langword="true"/> when the character is bold.</returns>
    private static bool IsBold(PdfiumTextPageHandle textPage, int index, Dictionary<nint, bool> fonts)
    {
        if (NativeMethods.FPDFText_GetFontWeight(textPage, index) >= BoldWeight)
        {
            return true;
        }

        var textObject = NativeMethods.FPDFText_GetTextObject(textPage, index);
        var font = textObject == 0 ? 0 : NativeMethods.FPDFTextObj_GetFont(textObject);
        if (font == 0)
        {
            return false;
        }

        ref var bold = ref CollectionsMarshal.GetValueRefOrAddDefault(fonts, font, out var known);
        if (!known)
        {
            bold = IsBoldFont(font);
        }

        return bold;
    }

    /// <summary>Determines whether a font is bold from its descriptor and name.</summary>
    /// <param name="font">The font.</param>
    /// <returns><see langword="true"/> for a bold font.</returns>
    private static unsafe bool IsBoldFont(nint font)
    {
        var flags = NativeMethods.FPDFFont_GetFlags(font);
        if (NativeMethods.FPDFFont_GetWeight(font) >= BoldWeight || (flags > 0 && (flags & ForceBoldFlag) != 0))
        {
            return true;
        }

        Span<byte> name = stackalloc byte[FontNameBytes];
        nuint length;
        fixed (byte* buffer = name)
        {
            length = NativeMethods.FPDFFont_GetBaseFontName(font, buffer, FontNameBytes);
        }

        var text = name[..(int)Math.Min(length, FontNameBytes)];
        return text.IndexOf("Bold"u8) >= 0 || text.IndexOf("Black"u8) >= 0 || text.IndexOf("Heavy"u8) >= 0
            || text.IndexOf("Semibold"u8) >= 0 || text.IndexOf("SemiBold"u8) >= 0 || text.IndexOf("Demi"u8) >= 0;
    }
}
