// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>PDFium entry points for reading and rewriting annotation geometry, used to move, resize and restyle annotations.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>Native <c>FPDFAnnot_GetFlags</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The annotation flags.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_GetFlags(nint annotation);

    /// <summary>Native <c>FPDFAnnot_GetBorder</c> entry point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="horizontalRadius">The horizontal corner radius.</param>
    /// <param name="verticalRadius">The vertical corner radius.</param>
    /// <param name="width">The border (stroke) width.</param>
    /// <returns>Non-zero when the annotation has a border.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_GetBorder(nint annotation, out float horizontalRadius, out float verticalRadius, out float width);

    /// <summary>Native <c>FPDFAnnot_GetInkListCount</c> entry point.</summary>
    /// <param name="annotation">The ink annotation.</param>
    /// <returns>The number of strokes.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFAnnot_GetInkListCount(nint annotation);

    /// <summary>Native <c>FPDFAnnot_GetInkListPath</c> entry point.</summary>
    /// <param name="annotation">The ink annotation.</param>
    /// <param name="pathIndex">The stroke index.</param>
    /// <param name="buffer">Receives the points, or null to ask for the count.</param>
    /// <param name="length">The buffer length in points.</param>
    /// <returns>The number of points in the stroke.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFAnnot_GetInkListPath(nint annotation, CULong pathIndex, FsPointF* buffer, CULong length);

    /// <summary>Native <c>FPDFAnnot_RemoveInkList</c> entry point.</summary>
    /// <param name="annotation">The ink annotation.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAnnot_RemoveInkList(nint annotation);

    /// <summary>Native <c>FPDFAnnot_GetVertices</c> entry point.</summary>
    /// <param name="annotation">The polygon or polyline annotation.</param>
    /// <param name="buffer">Receives the points, or null to ask for the count.</param>
    /// <param name="length">The buffer length in points.</param>
    /// <returns>The number of vertices.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFAnnot_GetVertices(nint annotation, FsPointF* buffer, CULong length);

    /// <summary>Native <c>FPDFPageObj_GetType</c> entry point.</summary>
    /// <param name="pageObject">The page object.</param>
    /// <returns>1 for text, 2 for a path, 3 for an image, and so on.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPageObj_GetType(nint pageObject);

    /// <summary>Native <c>FPDFPageObj_GetStrokeColor</c> entry point.</summary>
    /// <param name="pageObject">The page object.</param>
    /// <param name="red">The red channel.</param>
    /// <param name="green">The green channel.</param>
    /// <param name="blue">The blue channel.</param>
    /// <param name="alpha">The opacity.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPageObj_GetStrokeColor(nint pageObject, out uint red, out uint green, out uint blue, out uint alpha);

    /// <summary>Native <c>FPDFPageObj_GetFillColor</c> entry point.</summary>
    /// <param name="pageObject">The page object.</param>
    /// <param name="red">The red channel.</param>
    /// <param name="green">The green channel.</param>
    /// <param name="blue">The blue channel.</param>
    /// <param name="alpha">The opacity.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFPageObj_GetFillColor(nint pageObject, out uint red, out uint green, out uint blue, out uint alpha);
}
