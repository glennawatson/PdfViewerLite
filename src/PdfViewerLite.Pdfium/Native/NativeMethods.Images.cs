// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>Image objects copied into signature annotations.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>Creates an image object owned by the caller until appended.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The image object, or zero.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFPageObj_NewImageObj(PdfiumDocumentHandle document);

    /// <summary>Copies bitmap pixels and alpha into an image object.</summary>
    /// <param name="pages">Loaded pages whose cached image must be cleared, or null for a new image.</param>
    /// <param name="count">The number of loaded pages.</param>
    /// <param name="imageObject">The image object.</param>
    /// <param name="bitmap">The source bitmap.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFImageObj_SetBitmap(nint* pages, int count, nint imageObject, nint bitmap);
}
