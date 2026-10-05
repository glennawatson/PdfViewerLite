// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>PDFium entry points for editing annotations and page content, and for saving.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>Flattens printable annotations and filled fields into page content.</summary>
    /// <param name="page">The page.</param>
    /// <param name="usage">One for printing, zero for normal display.</param>
    /// <returns>Zero on failure, one on success, or two when there is nothing to flatten.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPage_Flatten(PdfiumPageHandle page, int usage);

    /// <summary>Transforms page content and clips it to the supplied rectangle.</summary>
    /// <param name="page">The page.</param>
    /// <param name="matrix">The transform.</param>
    /// <param name="clip">The resulting clip rectangle.</param>
    /// <returns>Nonzero when transformed.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPage_TransFormWithClip(PdfiumPageHandle page, in FsMatrix matrix, in FsRectF clip);

    /// <summary>Gets the page's crop box.</summary>
    /// <param name="page">The page.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <param name="right">The right edge.</param>
    /// <param name="top">The top edge.</param>
    /// <returns>Nonzero when the box exists.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPage_GetCropBox(PdfiumPageHandle page, out float left, out float bottom, out float right, out float top);

    /// <summary>Gets a page's stored clockwise rotation in quarter turns.</summary>
    /// <param name="page">The page.</param>
    /// <returns>Zero through three, or minus one on failure.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPage_GetRotation(PdfiumPageHandle page);

    /// <summary>Native <c>FPDFPage_GetAnnotCount</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The number of annotations.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPage_GetAnnotCount(PdfiumPageHandle page);

    /// <summary>Native <c>FPDFPage_GetAnnot</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns>The annotation, which must be closed with <see cref="FPDFPage_CloseAnnot"/>.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFPage_GetAnnot(PdfiumPageHandle page, int index);

    /// <summary>Native <c>FPDFPage_CreateAnnot</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="subtype">The annotation subtype.</param>
    /// <returns>The annotation, which must be closed with <see cref="FPDFPage_CloseAnnot"/>.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFPage_CreateAnnot(PdfiumPageHandle page, int subtype);

    /// <summary>Native <c>FPDFPage_CloseAnnot</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    [LibraryImport(Library)]
    internal static partial void FPDFPage_CloseAnnot(nint annotation);

    /// <summary>Native <c>FPDFPage_GetAnnotIndex</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The index, or -1.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPage_GetAnnotIndex(PdfiumPageHandle page, nint annotation);

    /// <summary>Native <c>FPDFPage_RemoveAnnot</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPage_RemoveAnnot(PdfiumPageHandle page, int index);

    /// <summary>Native <c>FPDFAnnot_GetSubtype</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The subtype.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_GetSubtype(nint annotation);

    /// <summary>Native <c>FPDFAnnot_GetRect</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="rect">The rectangle in PDF user space.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_GetRect(nint annotation, out FsRectF rect);

    /// <summary>Native <c>FPDFAnnot_SetRect</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="rect">The rectangle in PDF user space.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_SetRect(nint annotation, in FsRectF rect);

    /// <summary>Native <c>FPDFAnnot_GetColor</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="type">0 for the colour, 1 for the interior colour.</param>
    /// <param name="red">The red channel.</param>
    /// <param name="green">The green channel.</param>
    /// <param name="blue">The blue channel.</param>
    /// <param name="alpha">The opacity.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_GetColor(nint annotation, int type, out uint red, out uint green, out uint blue, out uint alpha);

    /// <summary>Native <c>FPDFAnnot_SetColor</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="type">0 for the colour, 1 for the interior colour.</param>
    /// <param name="red">The red channel.</param>
    /// <param name="green">The green channel.</param>
    /// <param name="blue">The blue channel.</param>
    /// <param name="alpha">The opacity.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_SetColor(nint annotation, int type, uint red, uint green, uint blue, uint alpha);

    /// <summary>Native <c>FPDFAnnot_AppendAttachmentPoints</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="quad">The quadrilateral.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_AppendAttachmentPoints(nint annotation, in FsQuadPointsF quad);

    /// <summary>Native <c>FPDFAnnot_AddInkStroke</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="points">The points.</param>
    /// <param name="count">The number of points.</param>
    /// <returns>The stroke index, or -1.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_AddInkStroke(nint annotation, FsPointF* points, nuint count);

    /// <summary>Native <c>FPDFAnnot_SetBorder</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="horizontalRadius">The horizontal corner radius.</param>
    /// <param name="verticalRadius">The vertical corner radius.</param>
    /// <param name="width">The border (stroke) width.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_SetBorder(nint annotation, float horizontalRadius, float verticalRadius, float width);

    /// <summary>Native <c>FPDFAnnot_SetFlags</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="flags">The annotation flags.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_SetFlags(nint annotation, int flags);

    /// <summary>Native <c>FPDFAnnot_GetLinkedAnnot</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The ASCII key of the reference, such as <c>IRT</c>.</param>
    /// <returns>The linked annotation, to be closed, or zero.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFAnnot_GetLinkedAnnot(nint annotation, byte* key);

    /// <summary>Native <c>FPDFAnnot_SetStringValue</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The ASCII dictionary key.</param>
    /// <param name="value">The null terminated UTF-16LE value.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_SetStringValue(nint annotation, byte* key, char* value);

    /// <summary>Native <c>FPDFAnnot_GetStringValue</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The ASCII dictionary key.</param>
    /// <param name="buffer">The UTF-16LE output buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The required length in bytes including the terminator.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFAnnot_GetStringValue(nint annotation, byte* key, void* buffer, CULong length);

    /// <summary>Native <c>FPDFAnnot_SetAP</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="mode">The appearance mode; 0 is normal.</param>
    /// <param name="value">The appearance stream, or null to clear every appearance so PDFium regenerates it.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_SetAP(nint annotation, int mode, char* value);

    /// <summary>Native <c>FPDFAnnot_AppendObject</c> entry point.</summary>
    /// <param name="annotation">The annotation (ink or stamp).</param>
    /// <param name="pageObject">The page object, now owned by the annotation.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_AppendObject(nint annotation, nint pageObject);

    /// <summary>Native <c>FPDFText_LoadStandardFont</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="font">The ASCII name of one of the 14 standard fonts.</param>
    /// <returns>The font, which must be closed with <see cref="FPDFFont_Close"/>.</returns>
    [LibraryImport(Library)]
    internal static partial PdfiumFontHandle FPDFText_LoadStandardFont(PdfiumDocumentHandle document, byte* font);

    /// <summary>Native <c>FPDFFont_Close</c> entry point.</summary>
    /// <param name="font">The font.</param>
    [LibraryImport(Library)]
    internal static partial void FPDFFont_Close(nint font);

    /// <summary>Native <c>FPDFPageObj_CreateTextObj</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="font">The font.</param>
    /// <param name="fontSize">The font size in points.</param>
    /// <returns>The text object.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFPageObj_CreateTextObj(PdfiumDocumentHandle document, PdfiumFontHandle font, float fontSize);

    /// <summary>Native <c>FPDFText_SetText</c> entry point.</summary>
    /// <param name="textObject">The text object.</param>
    /// <param name="text">The null terminated UTF-16LE text.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFText_SetText(nint textObject, char* text);

    /// <summary>Native <c>FPDFTextObj_SetTextRenderMode</c> entry point.</summary>
    /// <param name="textObject">The text object.</param>
    /// <param name="mode">The render mode; 3 is invisible.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFTextObj_SetTextRenderMode(nint textObject, int mode);

    /// <summary>Native <c>FPDFPageObj_Transform</c> entry point.</summary>
    /// <param name="pageObject">The page object.</param>
    /// <param name="a">Matrix a.</param>
    /// <param name="b">Matrix b.</param>
    /// <param name="c">Matrix c.</param>
    /// <param name="d">Matrix d.</param>
    /// <param name="e">Matrix e (x translation).</param>
    /// <param name="f">Matrix f (y translation).</param>
    [LibraryImport(Library)]
    internal static partial void FPDFPageObj_Transform(nint pageObject, double a, double b, double c, double d, double e, double f);

    /// <summary>Native <c>FPDFPageObj_SetFillColor</c> entry point.</summary>
    /// <param name="pageObject">The page object.</param>
    /// <param name="red">The red channel.</param>
    /// <param name="green">The green channel.</param>
    /// <param name="blue">The blue channel.</param>
    /// <param name="alpha">The opacity.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPageObj_SetFillColor(nint pageObject, uint red, uint green, uint blue, uint alpha);

    /// <summary>Native <c>FPDFPageObj_CreateNewRect</c> entry point.</summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The bottom edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>A new path object.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFPageObj_CreateNewRect(float x, float y, float width, float height);

    /// <summary>Native <c>FPDFPageObj_SetStrokeColor</c> entry point.</summary>
    /// <param name="pageObject">The page object.</param>
    /// <param name="red">The red channel.</param>
    /// <param name="green">The green channel.</param>
    /// <param name="blue">The blue channel.</param>
    /// <param name="alpha">The opacity.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPageObj_SetStrokeColor(nint pageObject, uint red, uint green, uint blue, uint alpha);

    /// <summary>Native <c>FPDFPageObj_SetStrokeWidth</c> entry point.</summary>
    /// <param name="pageObject">The page object.</param>
    /// <param name="width">The line width.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPageObj_SetStrokeWidth(nint pageObject, float width);

    /// <summary>Native <c>FPDFPageObj_CreateNewPath</c> entry point.</summary>
    /// <param name="x">Where the path starts, across.</param>
    /// <param name="y">Where the path starts, up.</param>
    /// <returns>A new path object.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFPageObj_CreateNewPath(float x, float y);

    /// <summary>Native <c>FPDFPath_MoveTo</c> entry point.</summary>
    /// <param name="path">The path object.</param>
    /// <param name="x">Where the next part starts, across.</param>
    /// <param name="y">Where the next part starts, up.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPath_MoveTo(nint path, float x, float y);

    /// <summary>Native <c>FPDFPath_LineTo</c> entry point.</summary>
    /// <param name="path">The path object.</param>
    /// <param name="x">Where the line ends, across.</param>
    /// <param name="y">Where the line ends, up.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPath_LineTo(nint path, float x, float y);

    /// <summary>Native <c>FPDFPageObj_SetLineCap</c> entry point.</summary>
    /// <param name="pageObject">The page object.</param>
    /// <param name="lineCap">The cap: 0 butt, 1 round, 2 square.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPageObj_SetLineCap(nint pageObject, int lineCap);

    /// <summary>Native <c>FPDFPageObj_SetLineJoin</c> entry point.</summary>
    /// <param name="pageObject">The page object.</param>
    /// <param name="lineJoin">The join: 0 miter, 1 round, 2 bevel.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPageObj_SetLineJoin(nint pageObject, int lineJoin);

    /// <summary>Native <c>FPDFPath_SetDrawMode</c> entry point.</summary>
    /// <param name="path">The path object.</param>
    /// <param name="fillMode">0 for no fill.</param>
    /// <param name="stroke">Non-zero to stroke.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPath_SetDrawMode(nint path, int fillMode, int stroke);

    /// <summary>Native <c>FPDFPageObj_GetBounds</c> entry point.</summary>
    /// <param name="pageObject">The page object.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <param name="right">The right edge.</param>
    /// <param name="top">The top edge.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPageObj_GetBounds(nint pageObject, out float left, out float bottom, out float right, out float top);

    /// <summary>Native <c>FPDFPage_InsertObject</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="pageObject">The page object, now owned by the page.</param>
    [LibraryImport(Library)]
    internal static partial void FPDFPage_InsertObject(PdfiumPageHandle page, nint pageObject);

    /// <summary>Native <c>FPDFPage_GenerateContent</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPage_GenerateContent(PdfiumPageHandle page);

    /// <summary>Native <c>FPDFPageObj_Destroy</c> entry point.</summary>
    /// <param name="pageObject">A page object not owned by a page or annotation.</param>
    [LibraryImport(Library)]
    internal static partial void FPDFPageObj_Destroy(nint pageObject);

    /// <summary>Native <c>FPDF_CreateNewDocument</c> entry point.</summary>
    /// <returns>An empty document.</returns>
    [LibraryImport(Library)]
    internal static partial PdfiumDocumentHandle FPDF_CreateNewDocument();

    /// <summary>Native <c>FPDF_ImportPagesByIndex</c> entry point.</summary>
    /// <param name="destination">The document receiving the pages.</param>
    /// <param name="source">The document the pages come from.</param>
    /// <param name="pageIndices">Zero based page indices.</param>
    /// <param name="length">The number of indices.</param>
    /// <param name="insertAt">Where to insert in the destination.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDF_ImportPagesByIndex(PdfiumDocumentHandle destination, PdfiumDocumentHandle source, int* pageIndices, CULong length, int insertAt);

    /// <summary>Native <c>FPDFPage_New</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">Where the page goes.</param>
    /// <param name="width">The width in points.</param>
    /// <param name="height">The height in points.</param>
    /// <returns>The new, blank page.</returns>
    [LibraryImport(Library)]
    internal static partial PdfiumPageHandle FPDFPage_New(PdfiumDocumentHandle document, int pageIndex, double width, double height);

    /// <summary>Native <c>FPDFPage_GetMediaBox</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <param name="right">The right edge.</param>
    /// <param name="top">The top edge.</param>
    /// <returns>Non-zero when the page has a media box.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPage_GetMediaBox(PdfiumPageHandle page, out float left, out float bottom, out float right, out float top);

    /// <summary>Native <c>FPDFPage_SetMediaBox</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <param name="right">The right edge.</param>
    /// <param name="top">The top edge.</param>
    [LibraryImport(Library)]
    internal static partial void FPDFPage_SetMediaBox(PdfiumPageHandle page, float left, float bottom, float right, float top);

    /// <summary>Native <c>FPDFPage_SetCropBox</c> entry point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <param name="right">The right edge.</param>
    /// <param name="top">The top edge.</param>
    [LibraryImport(Library)]
    internal static partial void FPDFPage_SetCropBox(PdfiumPageHandle page, float left, float bottom, float right, float top);

    /// <summary>Native <c>FPDF_ImportNPagesToOne</c> entry point.</summary>
    /// <param name="source">The document whose pages are laid out.</param>
    /// <param name="sheetWidth">The sheet width in points.</param>
    /// <param name="sheetHeight">The sheet height in points.</param>
    /// <param name="columns">Pages across each sheet.</param>
    /// <param name="rows">Pages down each sheet.</param>
    /// <returns>A new document of sheets.</returns>
    [LibraryImport(Library)]
    internal static partial PdfiumDocumentHandle FPDF_ImportNPagesToOne(PdfiumDocumentHandle source, float sheetWidth, float sheetHeight, nuint columns, nuint rows);

    /// <summary>Native <c>FPDF_SaveAsCopy</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="fileWrite">The writer.</param>
    /// <param name="flags"><c>FPDF_INCREMENTAL</c> or <c>FPDF_NO_INCREMENTAL</c>.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDF_SaveAsCopy(PdfiumDocumentHandle document, FileWrite* fileWrite, int flags);

    /// <summary>Native <c>FPDF_GetSignatureCount</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The number of signatures, or -1.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDF_GetSignatureCount(PdfiumDocumentHandle document);

    /// <summary>Native <c>FPDFAnnot_GetObjectCount</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The number of page objects in the annotation's appearance.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_GetObjectCount(nint annotation);

    /// <summary>Native <c>FPDFAnnot_GetObject</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="index">The object index.</param>
    /// <returns>The page object, owned by the annotation.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFAnnot_GetObject(nint annotation, int index);

    /// <summary>Native <c>FPDFAnnot_UpdateObject</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="pageObject">A changed page object in the annotation.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_UpdateObject(nint annotation, nint pageObject);
}
