// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Document;

/// <content>Text extraction.</content>
public sealed partial class PdfDocument
{
    /// <summary>The most text pages kept per document.</summary>
    private const int TextPageCapacity = 16;

    /// <summary>The recently used text pages, made on first use.</summary>
    private PdfTextPageCache? _textPages;

    /// <summary>Gets the recently used text pages.</summary>
    internal PdfTextPageCache TextPages
    {
        get
        {
            if (Volatile.Read(ref _textPages) is { } existing)
            {
                return existing;
            }

            _ = Interlocked.CompareExchange(ref _textPages, new(TextPageCapacity), null);
            return Volatile.Read(ref _textPages)!;
        }
    }

    /// <summary>Gets a page's text, extracting it on first use and keeping recently used pages.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The text page.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    public PdfTextPage GetTextPage(int pageIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, PageCount);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return TextPages.GetOrAdd(pageIndex, this, static (index, document) => document.ExtractText(index));
    }

    /// <summary>Extracts a page's text without caching it.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The text page.</returns>
    internal PdfTextPage ExtractText(int pageIndex)
    {
        var page = GetPage(pageIndex);
        var builder = TextPageBuilder.Current;
        var device = builder.Device;
        device.Reset(page);
        try
        {
            RunContent(page, device);
            device.Finish();
            return builder.Build(page, IsRightToLeft());
        }
        finally
        {
            device.Clear();
        }
    }

    /// <summary>Runs a page's content into the text device, keeping the text read before damaged content stops it, as PDFium does.</summary>
    /// <param name="page">The page.</param>
    /// <param name="device">The text device.</param>
    private void RunContent(PdfPage page, TextDevice device)
    {
        using var interpreter = new ContentInterpreter(RenderCache, device, 0);
        try
        {
            interpreter.RunPage(page);
        }
        catch (Exception ex) when (ex is InvalidDataException or PdfException or ArgumentException or InvalidOperationException
            or IndexOutOfRangeException or NotSupportedException or FormatException or OverflowException)
        {
            // The runs collected so far still make a text page.
        }
    }

    /// <summary>Determines whether the viewer preferences ask for right-to-left reading order.</summary>
    /// <returns><see langword="true"/> when /Direction is /R2L.</returns>
    private bool IsRightToLeft()
    {
        if (Catalog.GetDictionary(KnownName.ViewerPreferences) is not { } preferences)
        {
            return false;
        }

        var names = Objects.Names;
        var direction = preferences.GetName(names.Intern("Direction"u8));
        return names.NameEquals(direction, "R2L"u8);
    }
}
