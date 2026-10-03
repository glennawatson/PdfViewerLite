// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>Where each character of a page is, for working out reading order.</summary>
public sealed partial class PdfiumDocument : ITextLayoutSource
{
    /// <summary>The font weight from which text counts as bold.</summary>
    private const int BoldWeight = 600;

    /// <inheritdoc/>
    public void GetCharacters(int pageIndex, List<PageCharacter> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var scope = PdfiumLibrary.EnterScope();
        var page = IsDisposed ? null : GetPage(pageIndex);
        var textPage = page?.TextPage;
        if (page is null || textPage is null)
        {
            return;
        }

        var count = NativeMethods.FPDFText_CountChars(textPage);
        _ = output.EnsureCapacity(output.Count + count);
        for (var i = 0; i < count; i++)
        {
            var value = (char)NativeMethods.FPDFText_GetUnicode(textPage, i);
            var generated = NativeMethods.FPDFText_IsGenerated(textPage, i) == 1;
            var bounds = !generated && NativeMethods.FPDFText_GetCharBox(textPage, i, out var left, out var right, out var bottom, out var top) != 0
                ? page.ToViewer(left, top, right, bottom)
                : default(PageRect);
            var size = (float)NativeMethods.FPDFText_GetFontSize(textPage, i);
            var bold = NativeMethods.FPDFText_GetFontWeight(textPage, i) >= BoldWeight;
            output.Add(new(value, bounds, size, bold, generated));
        }
    }
}
