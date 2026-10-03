// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>PDFium entry points for reading digital signatures.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>Native <c>FPDF_GetSignatureObject</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The signature index.</param>
    /// <returns>The signature, owned by the document.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDF_GetSignatureObject(PdfiumDocumentHandle document, int index);

    /// <summary>Native <c>FPDFSignatureObj_GetContents</c> entry point.</summary>
    /// <param name="signature">The signature.</param>
    /// <param name="buffer">The output buffer.</param>
    /// <param name="length">The buffer length.</param>
    /// <returns>The needed length.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFSignatureObj_GetContents(nint signature, void* buffer, CULong length);

    /// <summary>Native <c>FPDFSignatureObj_GetByteRange</c> entry point.</summary>
    /// <param name="signature">The signature.</param>
    /// <param name="buffer">The output buffer of integers.</param>
    /// <param name="length">The buffer length in integers.</param>
    /// <returns>The needed length in integers.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFSignatureObj_GetByteRange(nint signature, int* buffer, CULong length);

    /// <summary>Native <c>FPDFSignatureObj_GetSubFilter</c> entry point.</summary>
    /// <param name="signature">The signature.</param>
    /// <param name="buffer">The ASCII output buffer.</param>
    /// <param name="length">The buffer length.</param>
    /// <returns>The needed length including the terminator.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFSignatureObj_GetSubFilter(nint signature, byte* buffer, CULong length);

    /// <summary>Native <c>FPDFSignatureObj_GetReason</c> entry point.</summary>
    /// <param name="signature">The signature.</param>
    /// <param name="buffer">The UTF-16LE output buffer.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The needed length in bytes.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFSignatureObj_GetReason(nint signature, void* buffer, CULong length);

    /// <summary>Native <c>FPDFSignatureObj_GetTime</c> entry point.</summary>
    /// <param name="signature">The signature.</param>
    /// <param name="buffer">The ASCII output buffer.</param>
    /// <param name="length">The buffer length.</param>
    /// <returns>The needed length including the terminator.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFSignatureObj_GetTime(nint signature, byte* buffer, CULong length);
}
