// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>PDFium's structure tree queries, used to read tagged documents in their logical order.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>Native <c>FPDFCatalog_IsTagged</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <returns>Nonzero when the document is marked as tagged.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFCatalog_IsTagged(PdfiumDocumentHandle document);

    /// <summary>Native <c>FPDF_StructTree_GetForPage</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The page's structure tree, closed with <see cref="FPDF_StructTree_Close"/>; zero when it has none.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDF_StructTree_GetForPage(PdfiumPageHandle page);

    /// <summary>Native <c>FPDF_StructTree_Close</c> entry point.</summary>
    /// <param name="tree">The structure tree.</param>
    [LibraryImport(Library)]
    internal static partial void FPDF_StructTree_Close(nint tree);

    /// <summary>Native <c>FPDF_StructTree_CountChildren</c> entry point.</summary>
    /// <param name="tree">The structure tree.</param>
    /// <returns>The number of top-level elements.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDF_StructTree_CountChildren(nint tree);

    /// <summary>Native <c>FPDF_StructTree_GetChildAtIndex</c> entry point.</summary>
    /// <param name="tree">The structure tree.</param>
    /// <param name="index">The child.</param>
    /// <returns>The element, or zero.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDF_StructTree_GetChildAtIndex(nint tree, int index);

    /// <summary>Native <c>FPDF_StructElement_GetType</c> entry point: the element's type after role mapping.</summary>
    /// <param name="element">The element.</param>
    /// <param name="buffer">The UTF-16LE output buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The required length in bytes including the terminator.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDF_StructElement_GetType(nint element, void* buffer, CULong length);

    /// <summary>Native <c>FPDF_StructElement_GetAltText</c> entry point.</summary>
    /// <param name="element">The element.</param>
    /// <param name="buffer">The UTF-16LE output buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The required length in bytes including the terminator.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDF_StructElement_GetAltText(nint element, void* buffer, CULong length);

    /// <summary>Native <c>FPDF_StructElement_GetActualText</c> entry point.</summary>
    /// <param name="element">The element.</param>
    /// <param name="buffer">The UTF-16LE output buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The required length in bytes including the terminator.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDF_StructElement_GetActualText(nint element, void* buffer, CULong length);

    /// <summary>Native <c>FPDF_StructElement_CountChildren</c> entry point.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The number of children, elements and marked content alike.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDF_StructElement_CountChildren(nint element);

    /// <summary>Native <c>FPDF_StructElement_GetChildAtIndex</c> entry point.</summary>
    /// <param name="element">The element.</param>
    /// <param name="index">The child.</param>
    /// <returns>The child element, or zero when the child is marked content.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDF_StructElement_GetChildAtIndex(nint element, int index);

    /// <summary>Native <c>FPDF_StructElement_GetChildMarkedContentID</c> entry point.</summary>
    /// <param name="element">The element.</param>
    /// <param name="index">The child.</param>
    /// <returns>The marked content id of a marked content child, or -1.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDF_StructElement_GetChildMarkedContentID(nint element, int index);

    /// <summary>Native <c>FPDF_StructElement_GetMarkedContentIdCount</c> entry point.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The number of marked content ids directly in the element, or -1.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDF_StructElement_GetMarkedContentIdCount(nint element);

    /// <summary>Native <c>FPDF_StructElement_GetMarkedContentIdAtIndex</c> entry point.</summary>
    /// <param name="element">The element.</param>
    /// <param name="index">The id's position.</param>
    /// <returns>The marked content id, or -1.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDF_StructElement_GetMarkedContentIdAtIndex(nint element, int index);

    /// <summary>Native <c>FPDFText_GetTextObject</c> entry point.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="index">The character.</param>
    /// <returns>The text object that drew the character, or zero for a generated one.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFText_GetTextObject(PdfiumTextPageHandle textPage, int index);

    /// <summary>Native <c>FPDFPageObj_GetMarkedContentID</c> entry point.</summary>
    /// <param name="pageObject">The page object.</param>
    /// <returns>The marked content id the object is drawn in, or -1.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPageObj_GetMarkedContentID(nint pageObject);

    /// <summary>Native <c>FPDFTextObj_GetFont</c> entry point.</summary>
    /// <param name="textObject">The text object.</param>
    /// <returns>The font, owned by the page; zero when there is none.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFTextObj_GetFont(nint textObject);

    /// <summary>Native <c>FPDFFont_GetWeight</c> entry point.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The weight from the font descriptor, 400 normal and 700 bold, or -1.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFFont_GetWeight(nint font);

    /// <summary>Native <c>FPDFFont_GetFlags</c> entry point.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The font descriptor flags, or -1.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFFont_GetFlags(nint font);

    /// <summary>Native <c>FPDFFont_GetBaseFontName</c> entry point.</summary>
    /// <param name="font">The font.</param>
    /// <param name="buffer">The output buffer for the UTF-8 name.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The required length in bytes including the terminator.</returns>
    [LibraryImport(Library)]
    internal static partial nuint FPDFFont_GetBaseFontName(nint font, byte* buffer, nuint length);
}
