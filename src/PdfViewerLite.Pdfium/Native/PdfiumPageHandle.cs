// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Win32.SafeHandles;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>Owns an <c>FPDF_PAGE</c>.</summary>
internal sealed class PdfiumPageHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="PdfiumPageHandle"/> class.</summary>
    public PdfiumPageHandle()
        : base(true)
    {
    }

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        using (PdfiumLibrary.EnterScope())
        {
            NativeMethods.FPDF_ClosePage(handle);
        }

        return true;
    }
}
