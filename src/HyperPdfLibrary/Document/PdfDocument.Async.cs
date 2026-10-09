// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Text;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Document;

/// <content>
/// Async forms of the reading operations. Each loads the bytes it will need into the source's cache with async I/O, then
/// runs the synchronous core on them with the caller's token checked at the core's loop boundaries. A source held in
/// memory or mapped has nothing to load, so these complete at once without allocating.
/// </content>
public sealed partial class PdfDocument
{
    /// <summary>Opens a file with async I/O.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the open, including the work after the file is read.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The file cannot be read, is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<PdfDocument> OpenAsync(string path, string? password, CancellationToken cancellationToken) =>
        OpenWithAsync(path, password is null ? PdfOpenOptions.Default : new PdfOpenOptions { Password = password }, cancellationToken);

    /// <summary>Opens a document read from a seekable stream with async I/O.</summary>
    /// <param name="stream">The readable, seekable stream; it must stay open and unchanged until the document is disposed.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the open, including the work after the stream is read.</param>
    /// <returns>The document.</returns>
    /// <exception cref="ArgumentException">The stream cannot read or seek.</exception>
    /// <exception cref="PdfException">The stream is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<PdfDocument> OpenAsync(Stream stream, string? password, CancellationToken cancellationToken) =>
        OpenWithAsync(stream, password is null ? PdfOpenOptions.Default : new PdfOpenOptions { Password = password }, cancellationToken);

    /// <summary>Opens a document held in memory; there is nothing to wait for, so the result is ready at once.</summary>
    /// <param name="bytes">The file's bytes; kept, not copied, and must not change.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The bytes are not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<PdfDocument> OpenAsync(byte[] bytes, string? password, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return OpenWithAsync(new MemoryPdfByteSource(bytes), true, password is null ? PdfOpenOptions.Default : new PdfOpenOptions { Password = password }, cancellationToken);
    }

    /// <summary>
    /// Opens a file with options and async I/O. <see cref="PdfOpenOptions.Source"/> chooses how the file is read:
    /// <see cref="PdfSourceKind.Memory"/> reads it whole and <see cref="PdfSourceKind.Stream"/> loads it through a page
    /// cache, both without blocking a thread; mapped and automatic sources open as <see cref="OpenWith(string, PdfOpenOptions)"/> does.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels the open; it also stops later decoding of the document's streams.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The file cannot be read, is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfDocument> OpenWithAsync(string path, PdfOpenOptions options, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(options);
        PdfByteSource source;
        try
        {
            source = await PdfByteSources.OpenAsync(path, options.Source, options.CacheBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            throw new PdfException($"The file '{path}' could not be read.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PdfException($"The file '{path}' could not be read.", ex);
        }

        return await OpenWithAsync(source, true, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens a document read from a seekable stream with options and async I/O.</summary>
    /// <param name="stream">The readable, seekable stream; it must stay open and unchanged until the document is disposed.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels the open; it also stops later decoding of the document's streams.</param>
    /// <returns>The document.</returns>
    /// <exception cref="ArgumentException">The stream cannot read or seek.</exception>
    /// <exception cref="PdfException">The stream is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<PdfDocument> OpenWithAsync(Stream stream, PdfOpenOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        return OpenWithAsync(new StreamPdfByteSource(stream, options.CacheBytes, false), true, options, cancellationToken);
    }

    /// <summary>
    /// Opens a document from a byte source. The cancellation token replaces the options' token, so cancelling it stops the
    /// open and any later decoding of the document's streams.
    /// </summary>
    /// <param name="source">The file.</param>
    /// <param name="ownsSource">Whether the document disposes the source, including when opening fails or is cancelled.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The file is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask<PdfDocument> OpenWithAsync(PdfByteSource source, bool ownsSource, PdfOpenOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        var bound = cancellationToken.CanBeCanceled ? options with { CancellationToken = cancellationToken } : options;
        PdfObjectStore objects;
        try
        {
            await PdfPrefetcher.PrefetchFileAsync(source, cancellationToken).ConfigureAwait(false);
            objects = OpenObjects(source, ownsSource, bound, cancellationToken);
        }
        catch
        {
            if (ownsSource)
            {
                source.Dispose();
            }

            throw;
        }

        try
        {
            if (source.NeedsPrefetch && objects.Catalog.GetDictionary(KnownName.Pages) is { } tree)
            {
                await PdfPrefetcher.PrefetchAsync(objects, tree, PdfPrefetchKind.PageTree, cancellationToken).ConfigureAwait(false);
            }

            return CreateScoped(objects, cancellationToken);
        }
        catch
        {
            objects.Dispose();
            throw;
        }
    }

    /// <summary>Loads what a page needs, so that reading or rendering it does not wait on the file.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the page's objects are cached.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public ValueTask PrefetchPageAsync(int pageIndex, CancellationToken cancellationToken)
    {
        var page = GetPage(pageIndex);
        return cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled(cancellationToken)
            : PdfPrefetcher.PrefetchAsync(Objects, page.Dictionary, PdfPrefetchKind.Page, cancellationToken);
    }

    /// <summary>Gets a page, loading what it needs first.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public ValueTask<PdfPage> GetPageAsync(int pageIndex, CancellationToken cancellationToken)
    {
        var page = GetPage(pageIndex);
        var load = PrefetchPageAsync(pageIndex, cancellationToken);
        return load.IsCompletedSuccessfully ? new(page) : PageAfterAsync(load, page);
    }

    /// <summary>Gets a page's text, extracting it on first use and stopping at the token's cancellation.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels the loading and the extraction.</param>
    /// <returns>The text page.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="ObjectDisposedException">The document has been disposed.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public ValueTask<PdfTextPage> GetTextPageAsync(int pageIndex, CancellationToken cancellationToken)
    {
        var load = PrefetchPageAsync(pageIndex, cancellationToken);
        return load.IsCompletedSuccessfully ? TextPageReady(pageIndex, cancellationToken) : TextPageAfterAsync(load, pageIndex, cancellationToken);
    }

    /// <summary>Walks the text of every page in order, loading each page just ahead of its extraction.</summary>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The text pages.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public async IAsyncEnumerable<PdfTextPage> GetTextPagesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 0; i < PageCount; i++)
        {
            yield return await GetTextPageAsync(i, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Searches every page for text, yielding the pages that match.</summary>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>One entry per page with at least one match, in page order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public IAsyncEnumerable<PdfPageMatches> FindAsync(string query, PdfTextSearchOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return FindPagesAsync(query, options, cancellationToken);
    }

    /// <summary>Gets the outline (bookmarks), loading its entries first.</summary>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>The root entries.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public ValueTask<IReadOnlyList<PdfOutlineItem>> GetOutlineAsync(CancellationToken cancellationToken)
    {
        var load = Catalog.GetDictionary(KnownName.Outlines) is { } root
            ? PdfPrefetcher.PrefetchAsync(Objects, root, PdfPrefetchKind.Outline, cancellationToken)
            : ValueTask.CompletedTask;
        return load.IsCompletedSuccessfully ? OutlineReady(cancellationToken) : OutlineAfterAsync(load, cancellationToken);
    }

    /// <summary>Gets a page's link annotations, loading the page first.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>The links that lead somewhere.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public ValueTask<IReadOnlyList<PdfLink>> GetLinksAsync(int pageIndex, CancellationToken cancellationToken)
    {
        var load = PrefetchPageAsync(pageIndex, cancellationToken);
        return load.IsCompletedSuccessfully ? LinksReady(pageIndex, cancellationToken) : LinksAfterAsync(load, pageIndex, cancellationToken);
    }

    /// <summary>Finds the annotations on a page that need a media player or a 3D viewer, loading the page first.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>The content found; none for a page that does not exist.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public ValueTask<PdfAnnotationContent> ScanAnnotationsAsync(int pageIndex, CancellationToken cancellationToken)
    {
        var load = (uint)pageIndex < (uint)PageCount ? PrefetchPageAsync(pageIndex, cancellationToken) : ValueTask.CompletedTask;
        return load.IsCompletedSuccessfully ? AnnotationsReady(pageIndex, cancellationToken) : AnnotationsAfterAsync(load, pageIndex, cancellationToken);
    }

    /// <summary>
    /// Saves the document with its edits appended to the original file, writing with async I/O. The original is copied
    /// from the source in chunks, then the update follows.
    /// </summary>
    /// <param name="destination">The stream to write.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when the bytes are written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">An edited value cannot be written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public async ValueTask SaveAsync(Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        await PdfIncrementalWriter.SaveAsync(Objects, destination, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the objects with the token in force for the synchronous core.</summary>
    /// <param name="source">The file.</param>
    /// <param name="ownsSource">Whether the objects own the source.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The objects.</returns>
    private static PdfObjectStore OpenObjects(PdfByteSource source, bool ownsSource, PdfOpenOptions options, CancellationToken cancellationToken)
    {
        using var scope = PdfCancellation.Enter(cancellationToken);
        return PdfObjectStore.OpenWith(source, ownsSource, options);
    }

    /// <summary>Makes the document, reading the page tree, with the token in force.</summary>
    /// <param name="objects">The objects.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The document.</returns>
    private static PdfDocument CreateScoped(PdfObjectStore objects, CancellationToken cancellationToken)
    {
        using var scope = PdfCancellation.Enter(cancellationToken);
        return Create(objects);
    }

    /// <summary>Determines whether an exception from the synchronous core belongs in the returned task rather than being thrown at the call.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> for cancellation and for a document that cannot be read.</returns>
    private static bool IsTaskFault(Exception exception) => exception is OperationCanceledException or PdfException;

    /// <summary>Waits for a page's load to finish, then hands the page back.</summary>
    /// <param name="load">The pending load.</param>
    /// <param name="page">The page that was being loaded.</param>
    /// <returns>The page.</returns>
    private static async ValueTask<PdfPage> PageAfterAsync(ValueTask load, PdfPage page)
    {
        await load.ConfigureAwait(false);
        return page;
    }

    /// <summary>Walks the pages, yielding those with matches.</summary>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>One entry per page with at least one match.</returns>
    private async IAsyncEnumerable<PdfPageMatches> FindPagesAsync(string query, PdfTextSearchOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var found = new List<PdfTextMatch>();
        for (var i = 0; i < PageCount; i++)
        {
            var text = await GetTextPageAsync(i, cancellationToken).ConfigureAwait(false);
            found.Clear();
            text.Find(query, options, found);
            if (found.Count > 0)
            {
                yield return new(i, [.. found]);
            }
        }
    }

    /// <summary>Extracts a page's text once its objects are loaded, reporting failures through the task.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The completed text page, or a faulted task.</returns>
    private ValueTask<PdfTextPage> TextPageReady(int pageIndex, CancellationToken cancellationToken)
    {
        try
        {
            return new(ExtractTextScoped(pageIndex, cancellationToken));
        }
        catch (Exception ex) when (IsTaskFault(ex))
        {
            return ValueTask.FromException<PdfTextPage>(ex);
        }
    }

    /// <summary>Waits for a load, then extracts a page's text.</summary>
    /// <param name="load">The load.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The text page.</returns>
    private async ValueTask<PdfTextPage> TextPageAfterAsync(ValueTask load, int pageIndex, CancellationToken cancellationToken)
    {
        await load.ConfigureAwait(false);
        return ExtractTextScoped(pageIndex, cancellationToken);
    }

    /// <summary>Reads the outline once its objects are loaded, reporting failures through the task.</summary>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The completed outline, or a faulted task.</returns>
    private ValueTask<IReadOnlyList<PdfOutlineItem>> OutlineReady(CancellationToken cancellationToken)
    {
        try
        {
            return new(ReadOutlineScoped(cancellationToken));
        }
        catch (Exception ex) when (IsTaskFault(ex))
        {
            return ValueTask.FromException<IReadOnlyList<PdfOutlineItem>>(ex);
        }
    }

    /// <summary>Waits for a load, then reads the outline.</summary>
    /// <param name="load">The load.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The outline.</returns>
    private async ValueTask<IReadOnlyList<PdfOutlineItem>> OutlineAfterAsync(ValueTask load, CancellationToken cancellationToken)
    {
        await load.ConfigureAwait(false);
        return ReadOutlineScoped(cancellationToken);
    }

    /// <summary>Reads a page's links once its objects are loaded, reporting failures through the task.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The completed links, or a faulted task.</returns>
    private ValueTask<IReadOnlyList<PdfLink>> LinksReady(int pageIndex, CancellationToken cancellationToken)
    {
        try
        {
            return new(ReadLinksScoped(pageIndex, cancellationToken));
        }
        catch (Exception ex) when (IsTaskFault(ex))
        {
            return ValueTask.FromException<IReadOnlyList<PdfLink>>(ex);
        }
    }

    /// <summary>Waits for a load, then reads a page's links.</summary>
    /// <param name="load">The load.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The links.</returns>
    private async ValueTask<IReadOnlyList<PdfLink>> LinksAfterAsync(ValueTask load, int pageIndex, CancellationToken cancellationToken)
    {
        await load.ConfigureAwait(false);
        return ReadLinksScoped(pageIndex, cancellationToken);
    }

    /// <summary>Scans a page's annotations once its objects are loaded, reporting failures through the task.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The completed content, or a faulted task.</returns>
    private ValueTask<PdfAnnotationContent> AnnotationsReady(int pageIndex, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(ScanAnnotations(pageIndex));
        }
        catch (Exception ex) when (IsTaskFault(ex))
        {
            return ValueTask.FromException<PdfAnnotationContent>(ex);
        }
    }

    /// <summary>Waits for a load, then scans a page's annotations.</summary>
    /// <param name="load">The load.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The content found.</returns>
    private async ValueTask<PdfAnnotationContent> AnnotationsAfterAsync(ValueTask load, int pageIndex, CancellationToken cancellationToken)
    {
        await load.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return ScanAnnotations(pageIndex);
    }

    /// <summary>Extracts a page's text with the token in force.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The text page.</returns>
    private PdfTextPage ExtractTextScoped(int pageIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var scope = PdfCancellation.Enter(cancellationToken);
        return GetTextPage(pageIndex);
    }

    /// <summary>Reads a page's links with the token in force.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The links.</returns>
    private IReadOnlyList<PdfLink> ReadLinksScoped(int pageIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var scope = PdfCancellation.Enter(cancellationToken);
        return GetLinks(pageIndex);
    }

    /// <summary>Reads the outline with the token in force.</summary>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The root entries.</returns>
    private IReadOnlyList<PdfOutlineItem> ReadOutlineScoped(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var scope = PdfCancellation.Enter(cancellationToken);
        return GetOutline();
    }
}
