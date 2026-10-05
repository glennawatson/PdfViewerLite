// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// A loaded PDFium page and, lazily, its text page and links. Every member must be called while holding the PDFium lock.
/// Coordinates are converted between PDF user space and the viewer's top-left page space through a virtual device so
/// that crop box offsets and intrinsic page rotation are honoured.
/// </summary>
[DebuggerDisplay("PdfiumPage: Page {Index}")]
internal sealed class PdfiumPage : IDisposable
{
    /// <summary>Virtual device pixels per point used for coordinate conversion.</summary>
    private const int DeviceScale = 64;

    /// <summary>The owning document handle.</summary>
    private readonly PdfiumDocumentHandle _document;

    /// <summary>The sizes of every page in the document, used to resolve link destinations.</summary>
    private readonly PageSize[] _documentPageSizes;

    /// <summary>The virtual device width.</summary>
    private readonly int _deviceWidth;

    /// <summary>The virtual device height.</summary>
    private readonly int _deviceHeight;

    /// <summary>The document's form, told when the page closes.</summary>
    private readonly PdfiumForm _form;

    /// <summary>The text page, once loaded.</summary>
    private PdfiumTextPageHandle? _textPage;

    /// <summary>The links, once loaded.</summary>
    private PageLink[]? _links;

    /// <summary>Initializes a new instance of the <see cref="PdfiumPage"/> class.</summary>
    /// <param name="document">The document handle.</param>
    /// <param name="index">The page index.</param>
    /// <param name="handle">The loaded page handle.</param>
    /// <param name="documentPageSizes">The sizes of every page in the document.</param>
    /// <param name="form">The document's form, told when the page closes.</param>
    internal PdfiumPage(PdfiumDocumentHandle document, int index, PdfiumPageHandle handle, PageSize[] documentPageSizes, PdfiumForm form)
    {
        _form = form;
        _document = document;
        _documentPageSizes = documentPageSizes;
        var size = documentPageSizes[index];
        Index = index;
        Handle = handle;
        Size = size;
        _deviceWidth = Math.Max(1, (int)MathF.Round(size.Width * DeviceScale));
        _deviceHeight = Math.Max(1, (int)MathF.Round(size.Height * DeviceScale));
    }

    /// <summary>Gets the page index.</summary>
    internal int Index { get; }

    /// <summary>Gets the native page handle.</summary>
    internal PdfiumPageHandle Handle { get; }

    /// <summary>Gets the page size.</summary>
    internal PageSize Size { get; }

    /// <summary>Gets the native text page, loading it on first use; <see langword="null"/> when the page has no text layer.</summary>
    internal PdfiumTextPageHandle? TextPage
    {
        get
        {
            if (_textPage is null)
            {
                var textPage = NativeMethods.FPDFText_LoadPage(Handle);
                if (textPage.IsInvalid)
                {
                    textPage.Dispose();
                    return null;
                }

                _textPage = textPage;
            }

            return _textPage;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _textPage?.Dispose();
        _textPage = null;
        _form.BeforeClose(Handle);
        Handle.Dispose();
    }

    /// <summary>Resolves a destination to a link target, approximating the location from the destination page's height.</summary>
    /// <param name="document">The document handle.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="pageSizes">The page sizes.</param>
    /// <returns>The target.</returns>
    internal static LinkTarget ResolveDestination(PdfiumDocumentHandle document, nint destination, PageSize[] pageSizes)
    {
        if (destination == 0)
        {
            return LinkTarget.None;
        }

        var page = NativeMethods.FPDFDest_GetDestPageIndex(document, destination);
        if ((uint)page >= (uint)pageSizes.Length)
        {
            return LinkTarget.None;
        }

        if (NativeMethods.FPDFDest_GetLocationInPage(destination, out var hasX, out var hasY, out _, out var x, out var y, out _) != 0 && (hasX != 0 || hasY != 0))
        {
            var location = new PagePoint(hasX != 0 ? x : 0, hasY != 0 ? pageSizes[page].Height - y : 0);
            return LinkTarget.ForPage(page, location);
        }

        return LinkTarget.ForPage(page);
    }

    /// <summary>Resolves an action to a link target.</summary>
    /// <param name="document">The document handle.</param>
    /// <param name="action">The action.</param>
    /// <param name="pageSizes">The page sizes.</param>
    /// <returns>The target.</returns>
    internal static unsafe LinkTarget ResolveAction(PdfiumDocumentHandle document, nint action, PageSize[] pageSizes)
    {
        if (action == 0)
        {
            return LinkTarget.None;
        }

        switch ((PdfActionType)NativeMethods.FPDFAction_GetType(action).Value)
        {
            case PdfActionType.GoTo:
            {
                return ResolveDestination(document, NativeMethods.FPDFAction_GetDest(document, action), pageSizes);
            }

            case PdfActionType.Uri:
            {
                var length = (int)NativeMethods.FPDFAction_GetURIPath(document, action, null, default).Value;
                if (length <= 1)
                {
                    return LinkTarget.None;
                }

                var buffer = new byte[length];
                fixed (byte* p = buffer)
                {
                    _ = NativeMethods.FPDFAction_GetURIPath(document, action, p, new((uint)length));
                }

                return LinkTarget.ForUri(NativeText.FromAscii(buffer));
            }

            default:
            {
                return LinkTarget.None;
            }
        }
    }

    /// <summary>Converts a PDF user space point to viewer page space.</summary>
    /// <param name="x">The PDF x coordinate.</param>
    /// <param name="y">The PDF y coordinate.</param>
    /// <returns>The viewer point.</returns>
    internal PagePoint ToViewer(double x, double y)
    {
        _ = NativeMethods.FPDF_PageToDevice(Handle, 0, 0, _deviceWidth, _deviceHeight, 0, x, y, out var deviceX, out var deviceY);
        return new((float)deviceX / DeviceScale, (float)deviceY / DeviceScale);
    }

    /// <summary>Converts a PDF user space rectangle to viewer page space.</summary>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="right">The right edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <returns>The viewer rectangle.</returns>
    internal PageRect ToViewer(double left, double top, double right, double bottom)
    {
        var a = ToViewer(left, top);
        var b = ToViewer(right, bottom);
        return PageRect.FromEdges(a.X, a.Y, b.X, b.Y);
    }

    /// <summary>Converts a viewer page space point to PDF user space.</summary>
    /// <param name="point">The viewer point.</param>
    /// <param name="x">The PDF x coordinate.</param>
    /// <param name="y">The PDF y coordinate.</param>
    internal void ToPdf(PagePoint point, out double x, out double y) =>
        _ = NativeMethods.FPDF_DeviceToPage(Handle, 0, 0, _deviceWidth, _deviceHeight, 0, (int)MathF.Round(point.X * DeviceScale), (int)MathF.Round(point.Y * DeviceScale), out x, out y);

    /// <summary>Forgets the text page and links after the page's text changed, so they are read again.</summary>
    internal void ResetText()
    {
        _textPage?.Dispose();
        _textPage = null;
        _links = null;
    }

    /// <summary>Gets the links on the page, loading them on first use.</summary>
    /// <returns>The links.</returns>
    internal PageLink[] GetLinks()
    {
        if (_links is not null)
        {
            return _links;
        }

        var links = new List<PageLink>();
        AddAnnotationLinks(links);
        AddWebLinks(links);
        _links = [.. links];
        return _links;
    }

    /// <summary>Adds the link annotations.</summary>
    /// <param name="links">The output list.</param>
    private void AddAnnotationLinks(List<PageLink> links)
    {
        var position = 0;
        while (NativeMethods.FPDFLink_Enumerate(Handle, ref position, out var link) != 0)
        {
            if (NativeMethods.FPDFLink_GetAnnotRect(link, out var rect) == 0)
            {
                continue;
            }

            var target = ResolveDestination(_document, NativeMethods.FPDFLink_GetDest(_document, link), _documentPageSizes);
            if (target.Kind == LinkTargetKind.None)
            {
                target = ResolveAction(_document, NativeMethods.FPDFLink_GetAction(link), _documentPageSizes);
            }

            if (target.Kind != LinkTargetKind.None)
            {
                links.Add(new(ToViewer(rect.Left, rect.Top, rect.Right, rect.Bottom), target));
            }
        }
    }

    /// <summary>Adds URLs written as plain text on the page.</summary>
    /// <param name="links">The output list.</param>
    private unsafe void AddWebLinks(List<PageLink> links)
    {
        var textPage = TextPage;
        if (textPage is null)
        {
            return;
        }

        var webLinks = NativeMethods.FPDFLink_LoadWebLinks(textPage);
        if (webLinks == 0)
        {
            return;
        }

        try
        {
            var count = NativeMethods.FPDFLink_CountWebLinks(webLinks);
            for (var i = 0; i < count; i++)
            {
                var length = NativeMethods.FPDFLink_GetURL(webLinks, i, null, 0);
                if (length <= 1)
                {
                    continue;
                }

                var chars = new char[length];
                fixed (char* p = chars)
                {
                    _ = NativeMethods.FPDFLink_GetURL(webLinks, i, p, length);
                }

                var target = LinkTarget.ForUri(new(chars, 0, length - 1));
                var rects = NativeMethods.FPDFLink_CountRects(webLinks, i);
                for (var r = 0; r < rects; r++)
                {
                    if (NativeMethods.FPDFLink_GetRect(webLinks, i, r, out var left, out var top, out var right, out var bottom) != 0)
                    {
                        links.Add(new(ToViewer(left, top, right, bottom), target));
                    }
                }
            }
        }
        finally
        {
            NativeMethods.FPDFLink_CloseWebLinks(webLinks);
        }
    }
}
