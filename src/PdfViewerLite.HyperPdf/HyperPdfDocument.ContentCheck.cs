// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms.Scripting;

namespace PdfViewerLite.HyperPdf;

/// <content>Finds content the viewer cannot show or run: XFA forms, scripts, multimedia and 3D.</content>
public sealed partial class HyperPdfDocument : IContentCheck
{
    /// <inheritdoc/>
    public UnsupportedContent CheckDocument()
    {
        if (IsDisposed)
        {
            return UnsupportedContent.None;
        }

        var content = PdfDocumentContent.HasXfaForm(_document) ? UnsupportedContent.XfaForm : UnsupportedContent.None;
        return PdfDocumentContent.GetJavaScriptActionCount(_document) > 0 ? content | UnsupportedContent.JavaScript : content;
    }

    /// <inheritdoc/>
    public UnsupportedContent CheckPage(int pageIndex)
    {
        if (IsDisposed || (uint)pageIndex >= (uint)PageCount)
        {
            return UnsupportedContent.None;
        }

        var found = PdfDocumentContent.ScanAnnotations(_document, pageIndex);
        var content = UnsupportedContent.None;
        if ((found & PdfAnnotationContent.Multimedia) != 0)
        {
            content |= UnsupportedContent.Multimedia;
        }

        if ((found & PdfAnnotationContent.ThreeD) != 0)
        {
            content |= UnsupportedContent.ThreeD;
        }

        return PdfDocumentContent.HasAcroForm(_document) && HasUnknownScripts(pageIndex) ? content | UnsupportedContent.JavaScript : content;
    }

    /// <summary>Determines whether a page has a field script that is not one of the formats and sums that are run.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <returns><see langword="true"/> when a script would be needed that is not run.</returns>
    private bool HasUnknownScripts(int pageIndex)
    {
        List<string> scripts = [];
        PdfDocumentContent.GetWidgetScripts(_document, pageIndex, scripts);
        foreach (var script in scripts)
        {
            if (ReferenceEquals(FormScript.Parse(script), FormScript.None))
            {
                return true;
            }
        }

        return false;
    }
}
