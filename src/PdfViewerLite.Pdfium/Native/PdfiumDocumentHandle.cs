// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Win32.SafeHandles;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>Owns an <c>FPDF_DOCUMENT</c>.</summary>
internal sealed class PdfiumDocumentHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="PdfiumDocumentHandle"/> class.</summary>
    public PdfiumDocumentHandle()
        : base(true)
    {
    }

    /// <summary>Gets or sets the file the document reads from, closed once the document is.</summary>
    internal PdfiumFileSource? Source { get; set; }

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        using (PdfiumLibrary.EnterScope())
        {
            NativeMethods.FPDF_CloseDocument(handle);
        }

        Source?.Dispose();
        return true;
    }
}
