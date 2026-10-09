// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.HyperPdf;

/// <summary>
/// A document opened with HyperPDF. The managed library answers every call, draws every page and holds every edit.
/// Safe to call from any thread.
/// </summary>
[DebuggerDisplay("HyperPdfDocument: {FilePath}")]
public sealed partial class HyperPdfDocument : IDocument
{
    /// <summary>The managed document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The page sizes, read once.</summary>
    private readonly PageSize[] _pageSizes;

    /// <summary>The outline, read on first use.</summary>
    private OutlineNode[]? _outline;

    /// <summary>The links of each page, read on first use.</summary>
    private PageLink[]?[]? _links;

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfDocument"/> class.</summary>
    /// <param name="document">The managed document.</param>
    /// <param name="filePath">The file path.</param>
    internal HyperPdfDocument(PdfDocument document, string filePath)
    {
        _document = document;
        FilePath = filePath;
        _pageSizes = new PageSize[document.PageCount];
        for (var i = 0; i < _pageSizes.Length; i++)
        {
            var page = PdfDocumentPages.GetPage(document, i);
            _pageSizes[i] = new(page.Width, page.Height);
        }
    }

    /// <inheritdoc/>
    public string FilePath { get; }

    /// <inheritdoc/>
    public int PageCount => _pageSizes.Length;

    /// <inheritdoc/>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Gets the managed document.</summary>
    internal PdfDocument Document => _document;

    /// <inheritdoc/>
    public PageSize[] GetPageSizes() => (PageSize[])_pageSizes.Clone();

    /// <inheritdoc/>
    public DocumentMetadata GetMetadata()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var info = PdfDocumentMetadata.GetInfo(_document);
        return new()
        {
            Title = info.Title,
            Author = info.Author,
            Subject = info.Subject,
            Keywords = info.Keywords,
            Creator = info.Creator,
            Producer = info.Producer,
            Created = info.Created,
            Modified = info.Modified,
            FormatVersion = info.Version,
            IsEncrypted = info.IsEncrypted,
        };
    }

    /// <inheritdoc/>
    public string? GetPageLabel(int pageIndex) => IsDisposed ? null : PdfDocumentLabels.GetPageLabel(_document, pageIndex);

    /// <inheritdoc/>
    public IReadOnlyList<OutlineNode> GetOutline()
    {
        if (IsDisposed)
        {
            return [];
        }

        var outline = Volatile.Read(ref _outline);
        if (outline is null)
        {
            outline = ConvertOutline(PdfDocumentNavigation.GetOutline(_document));
            Volatile.Write(ref _outline, outline);
        }

        return outline;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PageLink> GetLinks(int pageIndex)
    {
        if (IsDisposed || (uint)pageIndex >= (uint)PageCount)
        {
            return [];
        }

        if (Volatile.Read(ref _links) is null)
        {
            _ = Interlocked.CompareExchange(ref _links, new PageLink[]?[PageCount], null);
        }

        var cache = Volatile.Read(ref _links)!;
        var links = Volatile.Read(ref cache[pageIndex]);
        if (links is null)
        {
            links = ReadLinks(pageIndex);
            Volatile.Write(ref cache[pageIndex], links);
        }

        return links;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        ResetRenderer();
        _document.Dispose();
    }

    /// <summary>
    /// Reads a page's link annotations as viewer links, followed by the web and email addresses written as text, as
    /// the PDFium engine lists them.
    /// </summary>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The links.</returns>
    private PageLink[] ReadLinks(int pageIndex)
    {
        var links = ReadAnnotationLinks(pageIndex);
        GetWebLinksNative(pageIndex, links);
        return [.. links];
    }

    /// <summary>Reads a page's link annotations as viewer links.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The links.</returns>
    private List<PageLink> ReadAnnotationLinks(int pageIndex)
    {
        var page = PdfDocumentPages.GetPage(_document, pageIndex);
        var source = PdfDocumentLinks.GetLinks(_document, pageIndex);
        var links = new List<PageLink>(source.Count);
        foreach (var link in source)
        {
            var target = LinkTargets.From(_document, link.Action, pageIndex);
            if (target.Kind != LinkTargetKind.None)
            {
                links.Add(new(LinkTargets.ToPageRect(page.ToViewerRectangle(link.Bounds)), target));
            }
        }

        return links;
    }

    /// <summary>Converts outline entries.</summary>
    /// <param name="items">The library entries.</param>
    /// <returns>The viewer entries.</returns>
    private OutlineNode[] ConvertOutline(IReadOnlyList<PdfOutlineItem> items)
    {
        var nodes = new OutlineNode[items.Count];
        for (var i = 0; i < nodes.Length; i++)
        {
            var item = items[i];
            nodes[i] = new(item.Title, LinkTargets.From(_document, item.Action), ConvertOutline(item.Children), item.IsOpen);
        }

        return nodes;
    }
}
