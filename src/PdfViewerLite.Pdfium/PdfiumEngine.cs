// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>Opens PDF documents with PDFium.</summary>
[DebuggerDisplay("{Name}")]
public sealed class PdfiumEngine : IDocumentEngine
{
    /// <summary>How far into the file the signature may appear (some writers prefix junk).</summary>
    private const int SignatureSearchLength = 1024;

    /// <inheritdoc/>
    public string Name => "PDFium";

    /// <summary>Gets the PDF file signature.</summary>
    private static ReadOnlySpan<byte> PdfSignature => "%PDF-"u8;

    /// <inheritdoc/>
    public bool CanOpen(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[SignatureSearchLength];
            var read = stream.ReadAtLeast(header, header.Length, false);
            return header[..read].IndexOf(PdfSignature) >= 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public IDocument Open(string path, string? password)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var fullPath = Path.GetFullPath(path);
        using var scope = PdfiumLibrary.EnterScope();
        var handle = NativeMethods.FPDF_LoadDocument(fullPath, password);
        if (handle.IsInvalid)
        {
            var error = (int)NativeMethods.FPDF_GetLastError().Value;
            handle.Dispose();
            throw CreateOpenException(error, fullPath);
        }

        return new PdfiumDocument(handle, fullPath);
    }

    /// <summary>Maps a PDFium error code to an exception.</summary>
    /// <param name="error">The <c>FPDF_ERR_*</c> code.</param>
    /// <param name="path">The file path.</param>
    /// <returns>The exception.</returns>
    private static DocumentOpenException CreateOpenException(int error, string path) => error switch
    {
        (int)PdfiumError.File => new(DocumentOpenError.File, $"The file '{path}' could not be found or read."),
        (int)PdfiumError.Format => new(DocumentOpenError.Format, $"The file '{path}' is not a valid PDF document."),
        (int)PdfiumError.Password => new(DocumentOpenError.Password, "The document is password protected."),
        (int)PdfiumError.Security => new(DocumentOpenError.Security, "The document uses an unsupported security scheme."),
        _ => new(DocumentOpenError.Unknown, $"The document '{path}' could not be opened."),
    };
}
