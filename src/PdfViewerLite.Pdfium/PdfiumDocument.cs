// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Rendering;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>A PDF document backed by PDFium. Every member is serialised through the process wide PDFium lock.</summary>
[DebuggerDisplay("{FilePath} ({PageCount} pages)")]
public sealed class PdfiumDocument : IDocument
{
    /// <summary>The number of parsed pages kept loaded.</summary>
    private const int PageCacheSize = 8;

    /// <summary>The maximum outline depth followed, guarding against malformed files.</summary>
    private const int MaxOutlineDepth = 64;

    /// <summary>The maximum number of outline entries read, guarding against cyclic outlines.</summary>
    private const int MaxOutlineEntries = 20_000;

    /// <summary>The stack buffer size for short native strings.</summary>
    private const int StackBufferSize = 512;

    /// <summary>The PDFium BGRA bitmap format.</summary>
    private const int BitmapFormatBgra = 4;

    /// <summary>Opaque white in 0xAARRGGBB.</summary>
    private const uint White = 0xFFFFFFFFU;

    /// <summary>Divides the PDFium file version (for example 17) into major and minor parts.</summary>
    private const int VersionDivisor = 10;

    /// <summary>The native document.</summary>
    private readonly PdfiumDocumentHandle _handle;

    /// <summary>Loaded pages, most recently used last.</summary>
    private readonly List<PdfiumPage> _pages = new(PageCacheSize);

    /// <summary>The page sizes.</summary>
    private readonly PageSize[] _pageSizes;

    /// <summary>1 once the document has been disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="PdfiumDocument"/> class.</summary>
    /// <param name="handle">The open document handle.</param>
    /// <param name="filePath">The file path.</param>
    internal PdfiumDocument(PdfiumDocumentHandle handle, string filePath)
    {
        _handle = handle;
        FilePath = filePath;
        var count = Math.Max(0, NativeMethods.FPDF_GetPageCount(handle));
        _pageSizes = new PageSize[count];
        for (var i = 0; i < count; i++)
        {
            _pageSizes[i] = NativeMethods.FPDF_GetPageSizeByIndexF(handle, i, out var size) != 0 ? new(size.Width, size.Height) : default;
        }
    }

    /// <inheritdoc/>
    public string FilePath { get; }

    /// <inheritdoc/>
    public int PageCount => _pageSizes.Length;

    /// <inheritdoc/>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <inheritdoc/>
    public PageSize[] GetPageSizes() => (PageSize[])_pageSizes.Clone();

    /// <inheritdoc/>
    public DocumentMetadata GetMetadata()
    {
        using var scope = PdfiumLibrary.EnterScope();
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        var version = NativeMethods.FPDF_GetFileVersion(_handle, out var fileVersion) != 0
            ? string.Create(CultureInfo.InvariantCulture, $"{fileVersion / VersionDivisor}.{fileVersion % VersionDivisor}")
            : null;
        return new()
        {
            Title = GetMetaText("Title"),
            Author = GetMetaText("Author"),
            Subject = GetMetaText("Subject"),
            Keywords = GetMetaText("Keywords"),
            Creator = GetMetaText("Creator"),
            Producer = GetMetaText("Producer"),
            Created = PdfDate.Parse(GetMetaText("CreationDate")),
            Modified = PdfDate.Parse(GetMetaText("ModDate")),
            FormatVersion = version,
            IsEncrypted = NativeMethods.FPDF_GetSecurityHandlerRevision(_handle) >= 0,
        };
    }

    /// <inheritdoc/>
    public unsafe string? GetPageLabel(int pageIndex)
    {
        using var scope = PdfiumLibrary.EnterScope();
        if (IsDisposed)
        {
            return null;
        }

        var length = (int)NativeMethods.FPDF_GetPageLabel(_handle, pageIndex, null, default).Value;
        return length <= sizeof(char) ? null : ReadUtf16(length, (buffer, size) => NativeMethods.FPDF_GetPageLabel(_handle, pageIndex, buffer, size));
    }

    /// <inheritdoc/>
    public IReadOnlyList<OutlineNode> GetOutline()
    {
        using var scope = PdfiumLibrary.EnterScope();
        if (IsDisposed)
        {
            return [];
        }

        var budget = MaxOutlineEntries;
        return ReadOutline(0, 0, ref budget);
    }

    /// <inheritdoc/>
    public unsafe bool Render(in PageRenderInfo info, RenderTarget target)
    {
        using var scope = PdfiumLibrary.EnterScope();
        if (IsDisposed || (uint)info.PageIndex >= (uint)_pageSizes.Length)
        {
            return false;
        }

        var page = GetPage(info.PageIndex);
        if (page is null)
        {
            return false;
        }

        TileGrid.GetPagePixelSize(_pageSizes[info.PageIndex], info.Rotation, info.Scale, out var pageWidth, out var pageHeight);
        fixed (byte* pixels = target.Pixels)
        {
            var bitmap = NativeMethods.FPDFBitmap_CreateEx(target.Width, target.Height, BitmapFormatBgra, pixels, target.Stride);
            if (bitmap == 0)
            {
                return false;
            }

            try
            {
                _ = NativeMethods.FPDFBitmap_FillRect(bitmap, 0, 0, target.Width, target.Height, new(White));
                NativeMethods.FPDF_RenderPageBitmap(bitmap, page.Handle, -info.OffsetX, -info.OffsetY, pageWidth, pageHeight, (int)info.Rotation, ToNativeFlags(info.Flags));
            }
            finally
            {
                NativeMethods.FPDFBitmap_Destroy(bitmap);
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public int GetCharacterCount(int pageIndex)
    {
        using var scope = PdfiumLibrary.EnterScope();
        var textPage = GetTextPage(pageIndex);
        return textPage is null ? 0 : Math.Max(0, NativeMethods.FPDFText_CountChars(textPage));
    }

    /// <inheritdoc/>
    public int GetCharacterIndexAt(int pageIndex, PagePoint point, float tolerance)
    {
        using var scope = PdfiumLibrary.EnterScope();
        var page = IsDisposed ? null : GetPage(pageIndex);
        var textPage = page?.TextPage;
        if (page is null || textPage is null)
        {
            return -1;
        }

        page.ToPdf(point, out var x, out var y);
        var index = NativeMethods.FPDFText_GetCharIndexAtPos(textPage, x, y, tolerance, tolerance);
        return index >= 0 ? index : -1;
    }

    /// <inheritdoc/>
    public unsafe string GetText(int pageIndex, int start, int count)
    {
        using var scope = PdfiumLibrary.EnterScope();
        var textPage = GetTextPage(pageIndex);
        if (textPage is null || count <= 0)
        {
            return string.Empty;
        }

        var rented = ArrayPool<char>.Shared.Rent(count + 1);
        try
        {
            fixed (char* buffer = rented)
            {
                var written = NativeMethods.FPDFText_GetText(textPage, start, count, buffer);
                return written <= 1 ? string.Empty : new(rented, 0, written - 1);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }

    /// <inheritdoc/>
    public void GetTextBounds(int pageIndex, int start, int count, List<PageRect> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var scope = PdfiumLibrary.EnterScope();
        var page = IsDisposed ? null : GetPage(pageIndex);
        var textPage = page?.TextPage;
        if (page is null || textPage is null || count <= 0)
        {
            return;
        }

        var rects = NativeMethods.FPDFText_CountRects(textPage, start, count);
        for (var i = 0; i < rects; i++)
        {
            if (NativeMethods.FPDFText_GetRect(textPage, i, out var left, out var top, out var right, out var bottom) != 0)
            {
                output.Add(page.ToViewer(left, top, right, bottom));
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<PageLink> GetLinks(int pageIndex)
    {
        using var scope = PdfiumLibrary.EnterScope();
        var page = IsDisposed ? null : GetPage(pageIndex);
        return page is null ? [] : page.GetLinks();
    }

    /// <inheritdoc/>
    public unsafe void Find(int pageIndex, string query, SearchOptions options, List<TextMatch> output)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(output);
        if (query.Length == 0)
        {
            return;
        }

        using var scope = PdfiumLibrary.EnterScope();
        var textPage = GetTextPage(pageIndex);
        if (textPage is null)
        {
            return;
        }

        fixed (char* find = query)
        {
            var search = NativeMethods.FPDFText_FindStart(textPage, find, new((uint)options), 0);
            if (search == 0)
            {
                return;
            }

            try
            {
                while (NativeMethods.FPDFText_FindNext(search) != 0)
                {
                    output.Add(new(pageIndex, NativeMethods.FPDFText_GetSchResultIndex(search), NativeMethods.FPDFText_GetSchCount(search)));
                }
            }
            finally
            {
                NativeMethods.FPDFText_FindClose(search);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        using var scope = PdfiumLibrary.EnterScope();
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        foreach (var page in _pages)
        {
            page.Dispose();
        }

        _pages.Clear();
        _handle.Dispose();
    }

    /// <summary>Maps viewer render flags to PDFium render flags.</summary>
    /// <param name="flags">The viewer flags.</param>
    /// <returns>The PDFium flags.</returns>
    private static int ToNativeFlags(RenderFlags flags)
    {
        const int annotations = 0x01;
        const int grayscale = 0x08;
        const int printing = 0x800;
        var result = 0;
        if ((flags & RenderFlags.Annotations) != 0)
        {
            result |= annotations;
        }

        if ((flags & RenderFlags.Grayscale) != 0)
        {
            result |= grayscale;
        }

        if ((flags & RenderFlags.Printing) != 0)
        {
            result |= printing;
        }

        return result;
    }

    /// <summary>Reads a UTF-16 string through a two-call native API.</summary>
    /// <param name="length">The required length in bytes.</param>
    /// <param name="read">Fills the buffer.</param>
    /// <returns>The string.</returns>
    private static unsafe string ReadUtf16(int length, NativeStringReader read)
    {
        if (length <= StackBufferSize)
        {
            var stack = stackalloc byte[length];
            _ = read(stack, new((uint)length));
            return NativeText.FromUtf16(new(stack, length));
        }

        var rented = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            fixed (byte* buffer = rented)
            {
                _ = read(buffer, new((uint)length));
            }

            return NativeText.FromUtf16(rented.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Reads a metadata value.</summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The value, or <see langword="null"/> when empty.</returns>
    private unsafe string? GetMetaText(string tag)
    {
        var length = (int)NativeMethods.FPDF_GetMetaText(_handle, tag, null, default).Value;
        if (length <= sizeof(char))
        {
            return null;
        }

        var value = ReadUtf16(length, (buffer, size) => NativeMethods.FPDF_GetMetaText(_handle, tag, buffer, size));
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>Reads the children of an outline entry.</summary>
    /// <param name="parent">The parent bookmark, or zero for the root.</param>
    /// <param name="depth">The current depth.</param>
    /// <param name="budget">The remaining entry budget.</param>
    /// <returns>The children.</returns>
    private unsafe List<OutlineNode> ReadOutline(nint parent, int depth, ref int budget)
    {
        var result = new List<OutlineNode>();
        if (depth > MaxOutlineDepth)
        {
            return result;
        }

        var bookmark = NativeMethods.FPDFBookmark_GetFirstChild(_handle, parent);
        while (bookmark != 0 && budget > 0)
        {
            budget--;
            var current = bookmark;
            var length = (int)NativeMethods.FPDFBookmark_GetTitle(current, null, default).Value;
            var title = length <= sizeof(char) ? string.Empty : ReadUtf16(length, (buffer, size) => NativeMethods.FPDFBookmark_GetTitle(current, buffer, size));
            var target = PdfiumPage.ResolveDestination(_handle, NativeMethods.FPDFBookmark_GetDest(_handle, current), _pageSizes);
            if (target.Kind == LinkTargetKind.None)
            {
                target = PdfiumPage.ResolveAction(_handle, NativeMethods.FPDFBookmark_GetAction(current), _pageSizes);
            }

            var children = ReadOutline(current, depth + 1, ref budget);
            result.Add(new(title, target, children, NativeMethods.FPDFBookmark_GetCount(current) > 0));
            bookmark = NativeMethods.FPDFBookmark_GetNextSibling(_handle, current);
        }

        return result;
    }

    /// <summary>Gets the text page of a page, or <see langword="null"/>.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The text page.</returns>
    private PdfiumTextPageHandle? GetTextPage(int pageIndex) => IsDisposed ? null : GetPage(pageIndex)?.TextPage;

    /// <summary>Gets a loaded page from the cache, loading it when needed.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The page, or <see langword="null"/> when it cannot be loaded.</returns>
    private PdfiumPage? GetPage(int pageIndex)
    {
        if ((uint)pageIndex >= (uint)_pageSizes.Length)
        {
            return null;
        }

        var span = CollectionsMarshal.AsSpan(_pages);
        for (var i = span.Length - 1; i >= 0; i--)
        {
            var cached = span[i];
            if (cached.Index != pageIndex)
            {
                continue;
            }

            if (i != span.Length - 1)
            {
                _pages.RemoveAt(i);
                _pages.Add(cached);
            }

            return cached;
        }

        var handle = NativeMethods.FPDF_LoadPage(_handle, pageIndex);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }

        if (_pages.Count >= PageCacheSize)
        {
            _pages[0].Dispose();
            _pages.RemoveAt(0);
        }

        var page = new PdfiumPage(_handle, pageIndex, handle, _pageSizes);
        _pages.Add(page);
        return page;
    }
}
