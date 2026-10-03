// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>Embedded file entry points (<c>fpdf_attachment.h</c>).</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>Native <c>FPDFDoc_GetAttachmentCount</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The number of embedded files.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFDoc_GetAttachmentCount(PdfiumDocumentHandle document);

    /// <summary>Native <c>FPDFDoc_GetAttachment</c> entry point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The attachment index.</param>
    /// <returns>The attachment, owned by the document, or 0.</returns>
    [LibraryImport(Library)]
    internal static partial nint FPDFDoc_GetAttachment(PdfiumDocumentHandle document, int index);

    /// <summary>Native <c>FPDFAttachment_GetName</c> entry point.</summary>
    /// <param name="attachment">The attachment.</param>
    /// <param name="buffer">The UTF-16LE output, or null to measure.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <returns>The bytes needed, including the terminator.</returns>
    [LibraryImport(Library)]
    internal static partial CULong FPDFAttachment_GetName(nint attachment, byte* buffer, CULong length);

    /// <summary>Native <c>FPDFAttachment_GetFile</c> entry point.</summary>
    /// <param name="attachment">The attachment.</param>
    /// <param name="buffer">The output, or null to measure.</param>
    /// <param name="length">The buffer length in bytes.</param>
    /// <param name="needed">The file size in bytes.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFAttachment_GetFile(nint attachment, byte* buffer, CULong length, out CULong needed);

    /// <summary>Native <c>FPDFAttachment_GetFile</c> entry point, measuring only.</summary>
    /// <param name="attachment">The attachment.</param>
    /// <param name="buffer">Must be 0.</param>
    /// <param name="length">Must be 0.</param>
    /// <param name="needed">The file size in bytes.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library, EntryPoint = "FPDFAttachment_GetFile")]
    internal static partial int FPDFAttachment_GetFileSize(nint attachment, nint buffer, CULong length, out CULong needed);
}
