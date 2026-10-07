// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <content>Finds content PDFium cannot show or run: XFA forms, scripts, multimedia, 3D and portfolios.</content>
public sealed partial class PdfiumDocument : IContentCheck
{
    /// <summary>PDFium's form type for a full XFA form.</summary>
    private const int XfaFull = 2;

    /// <summary>PDFium's form type for an XFA form over AcroForm pages.</summary>
    private const int XfaForeground = 3;

    /// <summary>The sound annotation subtype.</summary>
    private const int SoundSubtype = 18;

    /// <summary>The movie annotation subtype.</summary>
    private const int MovieSubtype = 19;

    /// <summary>The screen (media player) annotation subtype.</summary>
    private const int ScreenSubtype = 21;

    /// <summary>The 3D annotation subtype.</summary>
    private const int ThreeDSubtype = 25;

    /// <summary>The rich media annotation subtype.</summary>
    private const int RichMediaSubtype = 26;

    /// <inheritdoc/>
    public UnsupportedContent CheckDocument()
    {
        using var scope = PdfiumLibrary.EnterScope();
        if (IsDisposed)
        {
            return UnsupportedContent.None;
        }

        var content = NativeMethods.FPDF_GetFormType(_handle) is XfaFull or XfaForeground ? UnsupportedContent.XfaForm : UnsupportedContent.None;
        if (NativeMethods.FPDFDoc_GetJavaScriptActionCount(_handle) > 0)
        {
            content |= UnsupportedContent.JavaScript;
        }

        return content;
    }

    /// <inheritdoc/>
    public UnsupportedContent CheckPage(int pageIndex)
    {
        using var scope = PdfiumLibrary.EnterScope();
        if (IsDisposed || (uint)pageIndex >= (uint)_pageSizes.Length)
        {
            return UnsupportedContent.None;
        }

        // Loaded on its own, so checking every page never pushes the pages being read out of the cache.
        using var page = NativeMethods.FPDF_LoadPage(_handle, pageIndex);
        if (page.IsInvalid)
        {
            return UnsupportedContent.None;
        }

        var content = UnsupportedContent.None;
        var count = NativeMethods.FPDFPage_GetAnnotCount(page);
        for (var i = 0; i < count; i++)
        {
            var annotation = NativeMethods.FPDFPage_GetAnnot(page, i);
            if (annotation == 0)
            {
                continue;
            }

            try
            {
                content |= Classify(annotation);
            }
            finally
            {
                NativeMethods.FPDFPage_CloseAnnot(annotation);
            }
        }

        return content;
    }

    /// <summary>Says what about one annotation cannot be shown or run.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The unsupported content it carries.</returns>
    private UnsupportedContent Classify(nint annotation) => NativeMethods.FPDFAnnot_GetSubtype(annotation) switch
    {
        SoundSubtype or MovieSubtype or ScreenSubtype or RichMediaSubtype => UnsupportedContent.Multimedia,
        ThreeDSubtype => UnsupportedContent.ThreeD,
        WidgetSubtype when _form.HasUnknownScripts(annotation) => UnsupportedContent.JavaScript,
        _ => UnsupportedContent.None,
    };
}
