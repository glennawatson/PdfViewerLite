// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>
/// Source generated PDFium entry points. PDFium is not thread safe: every call must be made while holding
/// the PDFium lock (see <see cref="PdfiumLibrary.EnterScope"/>). <c>unsigned long</c> parameters use <see cref="CULong"/> because its width differs
/// between Windows and Unix.
/// </summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The native library name.</summary>
    private const string Library = "pdfium";

    /// <summary>Native <c>FPDF_InitLibrary</c> entry point.</summary>
    [LibraryImport(Library)]
    internal static partial void FPDF_InitLibrary();

    /// <summary>Native <c>FPDF_GetLastError</c> entry point.</summary>
    /// <returns>The last error code.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDF_GetLastError();

    /// <summary>Native <c>FPDF_LoadCustomDocument</c> entry point.</summary>
    /// <param name="access">The <see cref="FileRead"/> structure, which must stay alive while the document is open.</param>
    /// <param name="password">The password.</param>
    /// <returns>The document handle.</returns>
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial PdfiumDocumentHandle FPDF_LoadCustomDocument(NativeBufferHandle access, string? password);

    /// <summary>Native <c>FPDF_LoadMemDocument64</c> entry point.</summary>
    /// <param name="data">The PDF bytes, which must stay alive while the document is open.</param>
    /// <param name="size">The byte count.</param>
    /// <param name="password">The password.</param>
    /// <returns>The document handle.</returns>
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial PdfiumDocumentHandle FPDF_LoadMemDocument64(NativeBufferHandle data, nuint size, string? password);

    /// <summary>Native <c>FPDF_CloseDocument</c> entry point.</summary>
    /// <param name="document">The document.</param>
    [LibraryImport(Library)]
    internal static partial void FPDF_CloseDocument(nint document);

    /// <summary>Native <c>FPDF_GetPageCount</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The page count.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDF_GetPageCount(PdfiumDocumentHandle document);

    /// <summary>Native <c>FPDF_GetPageSizeByIndexF</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="size">The size.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDF_GetPageSizeByIndexF(PdfiumDocumentHandle document, int pageIndex, out FsSizeF size);

    /// <summary>Native <c>FPDF_GetFileVersion</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="version">The version times ten.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDF_GetFileVersion(PdfiumDocumentHandle document, out int version);

    /// <summary>Native <c>FPDF_GetSecurityHandlerRevision</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The revision, or -1 when unencrypted.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDF_GetSecurityHandlerRevision(PdfiumDocumentHandle document);

    /// <summary>Native <c>FPDF_GetMetaText</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="tag">The UTF-8 tag.</param>
    /// <param name="buffer">The UTF-16LE output buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The required length in bytes including the terminator.</returns>
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial CULong FPDF_GetMetaText(PdfiumDocumentHandle document, string tag, void* buffer, CULong length);

    /// <summary>Native <c>FPDF_GetPageLabel</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The page.</param>
    /// <param name="buffer">The UTF-16LE output buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The required length in bytes including the terminator.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDF_GetPageLabel(PdfiumDocumentHandle document, int pageIndex, void* buffer, CULong length);

    /// <summary>Native <c>FPDF_LoadPage</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The page.</param>
    /// <returns>The page handle.</returns>
    [LibraryImport(Library)]
    internal static partial PdfiumPageHandle FPDF_LoadPage(PdfiumDocumentHandle document, int pageIndex);

    /// <summary>Native <c>FPDF_ClosePage</c> entry point.</summary>
    /// <param name="page">The page.</param>
    [LibraryImport(Library)]
    internal static partial void FPDF_ClosePage(nint page);

    /// <summary>Native <c>FPDF_PageToDevice</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="startX">The device origin x.</param>
    /// <param name="startY">The device origin y.</param>
    /// <param name="sizeX">The device width.</param>
    /// <param name="sizeY">The device height.</param>
    /// <param name="rotate">The rotation.</param>
    /// <param name="pageX">The page x.</param>
    /// <param name="pageY">The page y.</param>
    /// <param name="deviceX">The device x.</param>
    /// <param name="deviceY">The device y.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDF_PageToDevice(PdfiumPageHandle page, int startX, int startY, int sizeX, int sizeY, int rotate, double pageX, double pageY, out int deviceX, out int deviceY);

    /// <summary>Native <c>FPDF_DeviceToPage</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="startX">The device origin x.</param>
    /// <param name="startY">The device origin y.</param>
    /// <param name="sizeX">The device width.</param>
    /// <param name="sizeY">The device height.</param>
    /// <param name="rotate">The rotation.</param>
    /// <param name="deviceX">The device x.</param>
    /// <param name="deviceY">The device y.</param>
    /// <param name="pageX">The page x.</param>
    /// <param name="pageY">The page y.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDF_DeviceToPage(PdfiumPageHandle page, int startX, int startY, int sizeX, int sizeY, int rotate, int deviceX, int deviceY, out double pageX, out double pageY);

    /// <summary>Native <c>FPDFBitmap_CreateEx</c> entry point.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="format">The pixel format.</param>
    /// <param name="firstScan">The external buffer.</param>
    /// <param name="stride">The stride.</param>
    /// <returns>The bitmap handle.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFBitmap_CreateEx(int width, int height, int format, void* firstScan, int stride);

    /// <summary>Native <c>FPDFBitmap_FillRect</c> entry point.</summary>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The 0xAARRGGBB colour.</param>
    /// <returns>Non-zero on success in recent PDFium builds.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFBitmap_FillRect(nint bitmap, int left, int top, int width, int height, CULong color);

    /// <summary>Native <c>FPDFBitmap_Destroy</c> entry point.</summary>
    /// <param name="bitmap">The bitmap.</param>
    [LibraryImport(Library)]
    internal static partial void FPDFBitmap_Destroy(nint bitmap);

    /// <summary>Native <c>FPDF_RenderPageBitmap</c> entry point.</summary>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="page">The page.</param>
    /// <param name="startX">The page origin x in the bitmap.</param>
    /// <param name="startY">The page origin y in the bitmap.</param>
    /// <param name="sizeX">The full page width in pixels.</param>
    /// <param name="sizeY">The full page height in pixels.</param>
    /// <param name="rotate">The rotation in quarter turns.</param>
    /// <param name="flags">The render flags.</param>
    [LibraryImport(Library)]
    internal static partial void FPDF_RenderPageBitmap(nint bitmap, PdfiumPageHandle page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    /// <summary>Native <c>FPDFBookmark_GetFirstChild</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="bookmark">The parent bookmark, or zero for the root.</param>
    /// <returns>The first child.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFBookmark_GetFirstChild(PdfiumDocumentHandle document, nint bookmark);

    /// <summary>Native <c>FPDFBookmark_GetNextSibling</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="bookmark">The bookmark.</param>
    /// <returns>The next sibling.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFBookmark_GetNextSibling(PdfiumDocumentHandle document, nint bookmark);

    /// <summary>Native <c>FPDFBookmark_GetTitle</c> entry point.</summary>
    /// <param name="bookmark">The bookmark.</param>
    /// <param name="buffer">The UTF-16LE output buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The required length in bytes including the terminator.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFBookmark_GetTitle(nint bookmark, void* buffer, CULong length);

    /// <summary>Native <c>FPDFBookmark_GetCount</c> entry point.</summary>
    /// <param name="bookmark">The bookmark.</param>
    /// <returns>The child count; negative when closed.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFBookmark_GetCount(nint bookmark);

    /// <summary>Native <c>FPDFBookmark_GetDest</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="bookmark">The bookmark.</param>
    /// <returns>The destination.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFBookmark_GetDest(PdfiumDocumentHandle document, nint bookmark);

    /// <summary>Native <c>FPDFBookmark_GetAction</c> entry point.</summary>
    /// <param name="bookmark">The bookmark.</param>
    /// <returns>The action.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFBookmark_GetAction(nint bookmark);

    /// <summary>Native <c>FPDFAction_GetType</c> entry point.</summary>
    /// <param name="action">The action.</param>
    /// <returns>The action type.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFAction_GetType(nint action);

    /// <summary>Native <c>FPDFAction_GetDest</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action.</param>
    /// <returns>The destination.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFAction_GetDest(PdfiumDocumentHandle document, nint action);

    /// <summary>Native <c>FPDFAction_GetURIPath</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="action">The action.</param>
    /// <param name="buffer">The ASCII output buffer.</param>
    /// <param name="length">The buffer length.</param>
    /// <returns>The required length including the terminator.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFAction_GetURIPath(PdfiumDocumentHandle document, nint action, void* buffer, CULong length);

    /// <summary>Native <c>FPDFAction_GetFilePath</c> entry point.</summary>
    /// <param name="action">The launch or remote go-to action.</param>
    /// <param name="buffer">The UTF-8 output buffer.</param>
    /// <param name="length">The buffer length.</param>
    /// <returns>The required length including the terminator, or 0.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFAction_GetFilePath(nint action, void* buffer, CULong length);

    /// <summary>Native <c>FPDFDest_GetDestPageIndex</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">The destination.</param>
    /// <returns>The page index or -1.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFDest_GetDestPageIndex(PdfiumDocumentHandle document, nint destination);

    /// <summary>Native <c>FPDFDest_GetLocationInPage</c> entry point.</summary>
    /// <param name="destination">The destination.</param>
    /// <param name="hasX">Whether x is set.</param>
    /// <param name="hasY">Whether y is set.</param>
    /// <param name="hasZoom">Whether zoom is set.</param>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <param name="zoom">The zoom.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFDest_GetLocationInPage(nint destination, out int hasX, out int hasY, out int hasZoom, out float x, out float y, out float zoom);

    /// <summary>Native <c>FPDFLink_Enumerate</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="startPosition">The enumeration cursor.</param>
    /// <param name="link">The link.</param>
    /// <returns>Non-zero while links remain.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFLink_Enumerate(PdfiumPageHandle page, ref int startPosition, out nint link);

    /// <summary>Native <c>FPDFLink_GetDest</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="link">The link.</param>
    /// <returns>The destination.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFLink_GetDest(PdfiumDocumentHandle document, nint link);

    /// <summary>Native <c>FPDFLink_GetAction</c> entry point.</summary>
    /// <param name="link">The link.</param>
    /// <returns>The action.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFLink_GetAction(nint link);

    /// <summary>Native <c>FPDFLink_GetAnnotRect</c> entry point.</summary>
    /// <param name="link">The link.</param>
    /// <param name="rect">The rectangle.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFLink_GetAnnotRect(nint link, out FsRectF rect);

    /// <summary>Native <c>FPDFText_LoadPage</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The text page.</returns>
    [LibraryImport(Library)]
    internal static partial PdfiumTextPageHandle FPDFText_LoadPage(PdfiumPageHandle page);

    /// <summary>Native <c>FPDFText_ClosePage</c> entry point.</summary>
    /// <param name="textPage">The text page.</param>
    [LibraryImport(Library)]
    internal static partial void FPDFText_ClosePage(nint textPage);

    /// <summary>Native <c>FPDFText_CountChars</c> entry point.</summary>
    /// <param name="textPage">The text page.</param>
    /// <returns>The character count.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFText_CountChars(PdfiumTextPageHandle textPage);

    /// <summary>Native <c>FPDFText_GetText</c> entry point.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="start">The first character.</param>
    /// <param name="count">The character count.</param>
    /// <param name="result">The UTF-16 output buffer of count + 1 characters.</param>
    /// <returns>The characters written including the terminator.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFText_GetText(PdfiumTextPageHandle textPage, int start, int count, char* result);

    /// <summary>Native <c>FPDFText_GetCharIndexAtPos</c> entry point.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="x">The page x.</param>
    /// <param name="y">The page y.</param>
    /// <param name="horizontalTolerance">The x tolerance.</param>
    /// <param name="verticalTolerance">The y tolerance.</param>
    /// <returns>The index, -1 when none, -3 on error.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFText_GetCharIndexAtPos(PdfiumTextPageHandle textPage, double x, double y, double horizontalTolerance, double verticalTolerance);

    /// <summary>Native <c>FPDFText_CountRects</c> entry point.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="start">The first character.</param>
    /// <param name="count">The character count.</param>
    /// <returns>The rectangle count.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFText_CountRects(PdfiumTextPageHandle textPage, int start, int count);

    /// <summary>Native <c>FPDFText_GetRect</c> entry point.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="rectIndex">The rectangle.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="right">The right edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFText_GetRect(PdfiumTextPageHandle textPage, int rectIndex, out double left, out double top, out double right, out double bottom);

    /// <summary>Native <c>FPDFText_FindStart</c> entry point.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="findWhat">The null terminated UTF-16 query.</param>
    /// <param name="flags">The search flags.</param>
    /// <param name="startIndex">The first character.</param>
    /// <returns>The search handle.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFText_FindStart(PdfiumTextPageHandle textPage, char* findWhat, CULong flags, int startIndex);

    /// <summary>Native <c>FPDFText_FindNext</c> entry point.</summary>
    /// <param name="search">The search handle.</param>
    /// <returns>Non-zero when a match was found.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFText_FindNext(nint search);

    /// <summary>Native <c>FPDFText_GetSchResultIndex</c> entry point.</summary>
    /// <param name="search">The search handle.</param>
    /// <returns>The first character of the match.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFText_GetSchResultIndex(nint search);

    /// <summary>Native <c>FPDFText_GetSchCount</c> entry point.</summary>
    /// <param name="search">The search handle.</param>
    /// <returns>The match length.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFText_GetSchCount(nint search);

    /// <summary>Native <c>FPDFText_FindClose</c> entry point.</summary>
    /// <param name="search">The search handle.</param>
    [LibraryImport(Library)]
    internal static partial void FPDFText_FindClose(nint search);

    /// <summary>Native <c>FPDFLink_LoadWebLinks</c> entry point.</summary>
    /// <param name="textPage">The text page.</param>
    /// <returns>The web link handle.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFLink_LoadWebLinks(PdfiumTextPageHandle textPage);

    /// <summary>Native <c>FPDFLink_CountWebLinks</c> entry point.</summary>
    /// <param name="linkPage">The web link handle.</param>
    /// <returns>The link count.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFLink_CountWebLinks(nint linkPage);

    /// <summary>Native <c>FPDFLink_GetURL</c> entry point.</summary>
    /// <param name="linkPage">The web link handle.</param>
    /// <param name="linkIndex">The link.</param>
    /// <param name="buffer">The UTF-16 output buffer.</param>
    /// <param name="length">The buffer length in characters.</param>
    /// <returns>The characters required including the terminator.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFLink_GetURL(nint linkPage, int linkIndex, char* buffer, int length);

    /// <summary>Native <c>FPDFLink_CountRects</c> entry point.</summary>
    /// <param name="linkPage">The web link handle.</param>
    /// <param name="linkIndex">The link.</param>
    /// <returns>The rectangle count.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFLink_CountRects(nint linkPage, int linkIndex);

    /// <summary>Native <c>FPDFLink_GetRect</c> entry point.</summary>
    /// <param name="linkPage">The web link handle.</param>
    /// <param name="linkIndex">The link.</param>
    /// <param name="rectIndex">The rectangle.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="right">The right edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFLink_GetRect(nint linkPage, int linkIndex, int rectIndex, out double left, out double top, out double right, out double bottom);

    /// <summary>Native <c>FPDFLink_CloseWebLinks</c> entry point.</summary>
    /// <param name="linkPage">The web link handle.</param>
    [LibraryImport(Library)]
    internal static partial void FPDFLink_CloseWebLinks(nint linkPage);
}
