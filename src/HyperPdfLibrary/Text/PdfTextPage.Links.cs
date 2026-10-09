// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <content>Web and email addresses written as plain text.</content>
public sealed partial class PdfTextPage
{
    /// <summary>The web links, found on first use.</summary>
    private PdfWebLink[]? _webLinks;

    /// <summary>Gets the web and email addresses written on the page, as PDFium's FPDFLink_LoadWebLinks finds them.</summary>
    /// <returns>The links, in page order.</returns>
    public IReadOnlyList<PdfWebLink> GetWebLinks()
    {
        if (Volatile.Read(ref _webLinks) is { } existing)
        {
            return existing;
        }

        _ = Interlocked.CompareExchange(ref _webLinks, TextLinkParser.Extract(this), null);
        return Volatile.Read(ref _webLinks)!;
    }

    /// <summary>Appends the rectangles covering a web link, in user space.</summary>
    /// <param name="link">The link.</param>
    /// <param name="output">The list receiving the rectangles.</param>
    /// <exception cref="ArgumentNullException"><paramref name="link"/> or <paramref name="output"/> is null.</exception>
    public void GetWebLinkRects(PdfWebLink link, List<PdfRectangle> output)
    {
        ArgumentNullException.ThrowIfNull(link);
        GetRects(link.Start, link.Count, output);
    }
}
