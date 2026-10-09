// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// An open PDF document. The file is read through a <see cref="IO.PdfByteSource"/> as needed, never loaded whole;
/// objects are parsed on first use and cached, so opening a large file reads only its cross-reference table and page
/// tree. Safe to call from any thread.
/// </summary>
[DebuggerDisplay("PdfDocument: {PageCount} pages")]
public sealed partial class PdfDocument : IDisposable
{
    /// <summary>The pages and their indexes by object number; read again after an edit transaction ends.</summary>
    private PdfPageSet? _pageSet;

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="PdfDocument"/> class.</summary>
    /// <param name="objects">The document's objects.</param>
    private PdfDocument(PdfObjectStore objects)
    {
        Objects = objects;
        _pageSet = PdfPageSet.Read(objects);
        objects.SetChangeCallback(InvalidateCaches);
    }

    /// <summary>Gets the document's objects.</summary>
    public PdfObjectStore Objects { get; }

    /// <summary>Gets the document catalog.</summary>
    public PdfDictionary Catalog => Objects.Catalog;

    /// <summary>Gets the number of pages.</summary>
    public int PageCount => PageSet.Pages.Length;

    /// <summary>Gets a value indicating whether the document is encrypted.</summary>
    public bool IsEncrypted => Objects.Security is not null;

    /// <summary>Gets a value indicating whether the document has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Opens a file. The file is mapped read-only and read as needed, never loaded whole; it falls back to reading through
    /// a page cache when it cannot be mapped. The file stays open until the document is disposed.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The file cannot be read, is not a PDF, or the password is wrong.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfDocument Open(string path, string? password) =>
        OpenWith(path, password is null ? PdfOpenOptions.Default : new PdfOpenOptions { Password = password });

    /// <summary>Opens a document held in memory; the bytes are kept, not copied, and must not change.</summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The bytes are not a PDF, or the password is wrong.</exception>
    public static PdfDocument Open(byte[] bytes, string? password)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Create(PdfObjectStore.Open(bytes, password));
    }

    /// <summary>
    /// Opens a document read from a seekable stream through a bounded page cache. The stream is not disposed with the
    /// document and must stay open and unchanged until the document is disposed.
    /// </summary>
    /// <param name="stream">The readable, seekable stream.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <returns>The document.</returns>
    /// <exception cref="ArgumentException">The stream cannot read or seek.</exception>
    /// <exception cref="PdfException">The stream is not a PDF, or the password is wrong.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfDocument Open(Stream stream, string? password) =>
        OpenWith(stream, password is null ? PdfOpenOptions.Default : new PdfOpenOptions { Password = password });

    /// <summary>Gets a page.</summary>
    /// <param name="index">The zero based page index.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a page.</exception>
    public PdfPage GetPage(int index)
    {
        var pages = PageSet.Pages;
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)pages.Length, nameof(index));
        return pages[index];
    }

    /// <summary>Gets the index of a page object.</summary>
    /// <param name="id">The page object's id.</param>
    /// <returns>The zero based page index, or -1 when the object is not a page.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetPageIndex(PdfObjectId id) => PageSet.Indexes.GetValueOrDefault(id.Number, -1);

    /// <summary>Gets the document information.</summary>
    /// <returns>The information.</returns>
    public PdfDocumentInfo GetInfo()
    {
        var info = Objects.Trailer.GetDictionary(KnownName.Info);
        return new()
        {
            Title = Text(info, KnownName.Title),
            Author = Text(info, KnownName.Author),
            Subject = Text(info, KnownName.Subject),
            Keywords = Text(info, KnownName.Keywords),
            Creator = Text(info, KnownName.Creator),
            Producer = Text(info, KnownName.Producer),
            Created = info is null ? null : PdfDate.Parse(info.GetStringBytes(KnownName.CreationDate)),
            Modified = info is null ? null : PdfDate.Parse(info.GetStringBytes(KnownName.ModDate)),
            Version = Catalog.GetName(KnownName.Version) is { IsNone: false } version ? Objects.Names.GetString(version) : Objects.Version,
            IsEncrypted = IsEncrypted,
        };
    }

    /// <summary>
    /// Closes the file and empties the document's caches: the parsed objects, the page pictures of every renderer made for
    /// the document, the decoded images, fonts and other render caches, and the recent text pages. Work that is running on
    /// other threads fails with <see cref="ObjectDisposedException"/> or finishes with what it already holds; it never
    /// reads freed memory. The shared decoder scratch buffers are trimmed too.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        // Closing the objects first means nothing running can parse and re-fill the caches that are emptied next.
        Objects.Dispose();
        ReleaseRendering();
        ScratchPools.Trim();
    }

    /// <summary>Creates a document over opened objects, disposing them when reading the pages fails.</summary>
    /// <param name="objects">The objects, which the document owns.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Create(PdfObjectStore objects)
    {
        try
        {
            return new(objects);
        }
        catch
        {
            objects.Dispose();
            throw;
        }
    }

    /// <summary>Reads an information entry, treating blank text as missing.</summary>
    /// <param name="info">The information dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    private static string? Text(PdfDictionary? info, KnownName key)
    {
        var text = info?.GetText(key);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
