// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using PdfViewerLite.Core.Ocr;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// Writes recognised words onto a page as invisible text (render mode 3), each word scaled to the box it was found in,
/// so search, selection and copy work on scanned pages without changing how they look. Callers hold the PDFium lock.
/// The standard Helvetica font is used, which covers Western European text.
/// </summary>
internal static unsafe class PdfiumTextLayer
{
    /// <summary>PDFium's invisible text render mode.</summary>
    private const int Invisible = 3;

    /// <summary>Helvetica's ascent as a fraction of the font size.</summary>
    private const float Ascent = 0.718F;

    /// <summary>Helvetica's descent as a fraction of the font size.</summary>
    private const float Descent = 0.207F;

    /// <summary>The longest word copied on the stack.</summary>
    private const int StackChars = 128;

    /// <summary>Boxes smaller than this, in points, are noise.</summary>
    private const float MinimumSize = 0.5F;

    /// <summary>Adds the words to a page and regenerates its content.</summary>
    /// <param name="document">The document.</param>
    /// <param name="font">The font.</param>
    /// <param name="page">The page.</param>
    /// <param name="words">The words in page space.</param>
    /// <returns>The number of words written.</returns>
    internal static int Add(PdfiumDocumentHandle document, PdfiumFontHandle font, PdfiumPage page, ReadOnlySpan<OcrWord> words)
    {
        var written = 0;
        foreach (ref readonly var word in words)
        {
            var textObject = TryCreate(document, font, page, word);
            if (textObject == 0)
            {
                continue;
            }

            NativeMethods.FPDFPage_InsertObject(page.Handle, textObject);
            written++;
        }

        if (written > 0)
        {
            _ = NativeMethods.FPDFPage_GenerateContent(page.Handle);
            page.ResetText();
        }

        return written;
    }

    /// <summary>Creates an invisible text object filling a word's box.</summary>
    /// <param name="document">The document.</param>
    /// <param name="font">The font.</param>
    /// <param name="page">The page.</param>
    /// <param name="word">The word.</param>
    /// <returns>The text object, or 0 when the word cannot be placed.</returns>
    private static nint TryCreate(PdfiumDocumentHandle document, PdfiumFontHandle font, PdfiumPage page, in OcrWord word)
    {
        var bounds = word.Bounds;
        if (string.IsNullOrEmpty(word.Text) || bounds.Width < MinimumSize || bounds.Height < MinimumSize)
        {
            return 0;
        }

        page.ToPdf(new(bounds.Left, bounds.Top), out var x1, out var y1);
        page.ToPdf(new(bounds.Right, bounds.Bottom), out var x2, out var y2);
        var left = Math.Min(x1, x2);
        var bottom = Math.Min(y1, y2);
        var fontSize = (float)(Math.Abs(y2 - y1) / (Ascent + Descent));
        var textObject = NativeMethods.FPDFPageObj_CreateTextObj(document, font, fontSize);
        if (textObject == 0)
        {
            return 0;
        }

        if (!SetText(textObject, word.Text)
            || NativeMethods.FPDFPageObj_GetBounds(textObject, out var naturalLeft, out _, out var naturalRight, out _) == 0
            || naturalRight - naturalLeft <= 0)
        {
            NativeMethods.FPDFPageObj_Destroy(textObject);
            return 0;
        }

        _ = NativeMethods.FPDFTextObj_SetTextRenderMode(textObject, Invisible);
        var scale = Math.Abs(x2 - x1) / (naturalRight - naturalLeft);
        NativeMethods.FPDFPageObj_Transform(textObject, scale, 0, 0, 1, left - (naturalLeft * scale), bottom + (Descent * fontSize));
        return textObject;
    }

    /// <summary>Sets a text object's text from a null terminated copy.</summary>
    /// <param name="textObject">The text object.</param>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> on success.</returns>
    private static bool SetText(nint textObject, string text)
    {
        char[]? rented = null;
        var terminated = text.Length < StackChars ? stackalloc char[StackChars] : (rented = ArrayPool<char>.Shared.Rent(text.Length + 1));
        try
        {
            text.CopyTo(terminated);
            terminated[text.Length] = '\0';
            fixed (char* pointer = terminated)
            {
                return NativeMethods.FPDFText_SetText(textObject, pointer) != 0;
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
}
