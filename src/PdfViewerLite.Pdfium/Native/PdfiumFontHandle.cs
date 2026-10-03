// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Win32.SafeHandles;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>Owns an <c>FPDF_FONT</c> loaded into a document.</summary>
internal sealed class PdfiumFontHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="PdfiumFontHandle"/> class.</summary>
    public PdfiumFontHandle()
        : base(true)
    {
    }

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        using (PdfiumLibrary.EnterScope())
        {
            NativeMethods.FPDFFont_Close(handle);
        }

        return true;
    }
}
