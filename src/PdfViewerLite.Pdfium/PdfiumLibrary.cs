// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>Process wide PDFium state. PDFium is single threaded, so every call is serialised through one lock.</summary>
internal static class PdfiumLibrary
{
    /// <summary>The lock held for every PDFium call.</summary>
    private static readonly Lock Sync = new();

    /// <summary>Whether the library has been initialised.</summary>
    private static bool _initialized;

    /// <summary>Initializes static members of the <see cref="PdfiumLibrary"/> class.</summary>
    static PdfiumLibrary() => PdfiumLibraryResolver.Install();

    /// <summary>Enters the PDFium lock, initialising the library on first use.</summary>
    /// <returns>The scope to dispose when done.</returns>
    internal static Lock.Scope EnterScope()
    {
        var scope = Sync.EnterScope();
        if (!_initialized)
        {
            NativeMethods.FPDF_InitLibrary();
            _initialized = true;
        }

        return scope;
    }
}
