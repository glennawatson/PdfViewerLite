// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>PDFium's per-character text queries, used to work out reading order.</summary>
internal static partial class NativeMethods
{
    /// <summary>Native <c>FPDFText_GetUnicode</c> entry point.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="index">The character.</param>
    /// <returns>The UTF-16 code unit, or zero.</returns>
    [LibraryImport(Library)]
    internal static partial uint FPDFText_GetUnicode(PdfiumTextPageHandle textPage, int index);

    /// <summary>Native <c>FPDFText_GetCharBox</c> entry point.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="index">The character.</param>
    /// <param name="left">Receives the left edge.</param>
    /// <param name="right">Receives the right edge.</param>
    /// <param name="bottom">Receives the bottom edge.</param>
    /// <param name="top">Receives the top edge.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFText_GetCharBox(PdfiumTextPageHandle textPage, int index, out double left, out double right, out double bottom, out double top);

    /// <summary>Native <c>FPDFText_GetLooseCharBox</c> entry point: the box from the font's ascent to its descent.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="index">The character.</param>
    /// <param name="rect">Receives the box in page space.</param>
    /// <returns>Nonzero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFText_GetLooseCharBox(PdfiumTextPageHandle textPage, int index, out FsRectF rect);

    /// <summary>Native <c>FPDFText_GetFontSize</c> entry point.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="index">The character.</param>
    /// <returns>The font size in points.</returns>
    [LibraryImport(Library)]
    internal static partial double FPDFText_GetFontSize(PdfiumTextPageHandle textPage, int index);

    /// <summary>Native <c>FPDFText_GetFontWeight</c> entry point.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="index">The character.</param>
    /// <returns>The weight, 400 normal and 700 bold, or -1.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFText_GetFontWeight(PdfiumTextPageHandle textPage, int index);

    /// <summary>Native <c>FPDFText_IsGenerated</c> entry point.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="index">The character.</param>
    /// <returns>1 when PDFium inserted the character (a space or line break), 0 when it is in the page, -1 on error.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFText_IsGenerated(PdfiumTextPageHandle textPage, int index);
}
