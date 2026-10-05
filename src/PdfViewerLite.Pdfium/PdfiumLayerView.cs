// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// A copy of the document, held in memory, whose default layer visibility shows the layers the user chose. Pages are
/// drawn from it while any layer differs from the document's own setting. Callers hold the PDFium lock.
/// </summary>
[DebuggerDisplay("PdfiumLayerView: Layer view, {_pages.Count} pages loaded")]
internal sealed class PdfiumLayerView : IDisposable
{
    /// <summary>The number of pages kept loaded.</summary>
    private const int PageCacheSize = 8;

    /// <summary>The copy's bytes, which PDFium reads in place.</summary>
    private readonly NativeBufferHandle _buffer;

    /// <summary>The copy.</summary>
    private readonly PdfiumDocumentHandle _document;

    /// <summary>Loaded pages by index.</summary>
    private readonly Dictionary<int, PdfiumPageHandle> _pages = [];

    /// <summary>Initializes a new instance of the <see cref="PdfiumLayerView"/> class.</summary>
    /// <param name="buffer">The copy's bytes.</param>
    /// <param name="document">The copy.</param>
    private PdfiumLayerView(NativeBufferHandle buffer, PdfiumDocumentHandle document)
    {
        _buffer = buffer;
        _document = document;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var page in _pages.Values)
        {
            page.Dispose();
        }

        _pages.Clear();
        _document.Dispose();
        _buffer.Dispose();
    }

    /// <summary>Opens a copy from its bytes.</summary>
    /// <param name="bytes">The copy.</param>
    /// <returns>The view, or <see langword="null"/> when PDFium cannot open it.</returns>
    internal static PdfiumLayerView? Open(byte[] bytes)
    {
        var buffer = new NativeBufferHandle(bytes);
        var document = NativeMethods.FPDF_LoadMemDocument64(buffer, (nuint)buffer.Length, null);
        if (!document.IsInvalid)
        {
            return new(buffer, document);
        }

        document.Dispose();
        buffer.Dispose();
        return null;
    }

    /// <summary>Gets a page of the copy, loading it on first use.</summary>
    /// <param name="index">The page index.</param>
    /// <returns>The page, or <see langword="null"/>.</returns>
    internal PdfiumPageHandle? GetPage(int index)
    {
        if (_pages.TryGetValue(index, out var cached))
        {
            return cached;
        }

        if (_pages.Count >= PageCacheSize)
        {
            foreach (var page in _pages.Values)
            {
                page.Dispose();
            }

            _pages.Clear();
        }

        var loaded = NativeMethods.FPDF_LoadPage(_document, index);
        if (loaded.IsInvalid)
        {
            loaded.Dispose();
            return null;
        }

        _pages[index] = loaded;
        return loaded;
    }
}
