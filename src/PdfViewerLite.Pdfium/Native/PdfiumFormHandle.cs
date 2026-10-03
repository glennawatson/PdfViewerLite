// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>
/// Owns an <c>FPDF_FORMHANDLE</c> and the zeroed <c>FPDF_FORMFILLINFO</c> it was created with, which PDFium reads for
/// the environment's whole life. Every callback is left null; PDFium checks each one before calling it.
/// </summary>
internal sealed unsafe class PdfiumFormHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>The size of the zeroed <c>FPDF_FORMFILLINFO</c>, larger than the structure.</summary>
    private const int InfoBytes = 1024;

    /// <summary>The form fill interface version: stable callbacks only.</summary>
    private const int InterfaceVersion = 1;

    /// <summary>The <c>FPDF_FORMFILLINFO</c>.</summary>
    private void* _info;

    /// <summary>Initializes a new instance of the <see cref="PdfiumFormHandle"/> class.</summary>
    public PdfiumFormHandle()
        : base(true)
    {
    }

    /// <summary>Creates the form environment of a document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The handle; invalid when the document has no form.</returns>
    internal static PdfiumFormHandle Create(PdfiumDocumentHandle document)
    {
        var form = new PdfiumFormHandle();
        if (NativeMethods.FPDF_GetFormType(document) == 0)
        {
            return form;
        }

        form._info = NativeMemory.AllocZeroed(InfoBytes);
        *(int*)form._info = InterfaceVersion;
        form.SetHandle(NativeMethods.FPDFDOC_InitFormFillEnvironment(document, form._info));
        return form;
    }

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        using (PdfiumLibrary.EnterScope())
        {
            NativeMethods.FPDFDOC_ExitFormFillEnvironment(handle);
        }

        NativeMemory.Free(_info);
        _info = null;
        return true;
    }
}
