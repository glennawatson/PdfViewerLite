// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>Annotation editing and saving.</summary>
public sealed partial class PdfiumDocument
{
    /// <summary>Saves only the changes after the original bytes, keeping digital signatures valid.</summary>
    private const int SaveIncremental = 1;

    /// <summary>Saves a complete, compact file.</summary>
    private const int SaveFull = 2;

    /// <summary>The annotations of each page read so far, dropped when the page is edited.</summary>
    private readonly Dictionary<int, PageAnnotation[]> _annotationCache = [];

    /// <summary>The number of edits since opening or the last save.</summary>
    private int _unsavedChanges;

    /// <inheritdoc/>
    public bool HasUnsavedChanges => Volatile.Read(ref _unsavedChanges) != 0;

    /// <inheritdoc/>
    public void GetAnnotations(int pageIndex, List<PageAnnotation> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var scope = PdfiumLibrary.EnterScope();
        if (_annotationCache.TryGetValue(pageIndex, out var cached))
        {
            output.AddRange(cached);
            return;
        }

        if (IsDisposed || GetPage(pageIndex) is not { } page)
        {
            return;
        }

        var start = output.Count;
        PdfiumAnnotations.Read(page, output);
        _annotationCache[pageIndex] = [.. CollectionsMarshal.AsSpan(output)[start..]];
    }

    /// <inheritdoc/>
    public int AddMarkup(int pageIndex, AnnotationKind kind, ReadOnlySpan<PageRect> lines, uint color, string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page ? PdfiumAnnotations.AddMarkup(page, kind, lines, color, contents) : -1);
    }

    /// <inheritdoc/>
    public int AddInk(int pageIndex, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> strokeLengths, uint color, float width, AnnotationKind kind)
    {
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page ? PdfiumAnnotations.AddInk(page, points, strokeLengths, color, width, kind) : -1);
    }

    /// <inheritdoc/>
    public int AddNote(int pageIndex, PagePoint location, string contents, uint color)
    {
        ArgumentNullException.ThrowIfNull(contents);
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page ? PdfiumAnnotations.AddNote(page, location, contents, color) : -1);
    }

    /// <inheritdoc/>
    public int AddText(int pageIndex, PagePoint location, string text, float fontSize, uint color, AnnotationKind kind)
    {
        ArgumentNullException.ThrowIfNull(text);
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page ? PdfiumAnnotations.AddText(_fonts, page, location, text, fontSize, color, kind) : -1);
    }

    /// <inheritdoc/>
    public bool SetColor(int pageIndex, int index, uint color)
    {
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page && PdfiumAnnotations.SetColor(page, index, color));
    }

    /// <inheritdoc/>
    public bool SetContents(int pageIndex, int index, string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page && PdfiumAnnotations.SetContents(page, index, contents));
    }

    /// <inheritdoc/>
    public bool Remove(int pageIndex, int index)
    {
        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page && NativeMethods.FPDFPage_RemoveAnnot(page.Handle, index) != 0);
    }

    /// <inheritdoc/>
    public bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        using var scope = PdfiumLibrary.EnterScope();
        if (IsDisposed)
        {
            return false;
        }

        var flags = NativeMethods.FPDF_GetSignatureCount(_handle) > 0 ? SaveIncremental : SaveFull;
        if (!WriteDocument(_handle, destination, flags))
        {
            return false;
        }

        Volatile.Write(ref _unsavedChanges, 0);
        return true;
    }

    /// <summary>Writes a document into a stream. Callers hold the PDFium lock.</summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">The stream.</param>
    /// <param name="flags">Incremental or full.</param>
    /// <returns><see langword="true"/> when written.</returns>
    private static unsafe bool WriteDocument(PdfiumDocumentHandle document, Stream destination, int flags)
    {
        var stream = new GCHandle<Stream>(destination);
        try
        {
            var writer = new FileWrite(&WriteBlock, stream);
            return NativeMethods.FPDF_SaveAsCopy(document, &writer, flags) != 0;
        }
        finally
        {
            stream.Dispose();
        }
    }

    /// <summary>PDFium's write callback: copies a block into the destination stream.</summary>
    /// <param name="writer">The writer PDFium was given.</param>
    /// <param name="data">The block.</param>
    /// <param name="size">The block size in bytes.</param>
    /// <returns>1 on success, 0 to make PDFium stop.</returns>
    [UnmanagedCallersOnly]
    private static unsafe int WriteBlock(FileWrite* writer, void* data, CULong size)
    {
        try
        {
            ReadOnlySpan<byte> block = new(data, checked((int)size.Value));
            writer->Target.Write(block);
            return 1;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (OverflowException)
        {
            return 0;
        }
    }

    /// <summary>Gets a page for editing, or <see langword="null"/> when the document is closed.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The page.</returns>
    private PdfiumPage? EditablePage(int pageIndex) => IsDisposed ? null : GetPage(pageIndex);

    /// <summary>Records an edit when an index shows one was made.</summary>
    /// <param name="pageIndex">The edited page.</param>
    /// <param name="index">The new annotation index, or -1.</param>
    /// <returns>The index.</returns>
    private int Changed(int pageIndex, int index)
    {
        _ = Changed(pageIndex, index >= 0);
        return index;
    }

    /// <summary>Records an edit when it succeeded, forgetting the page's cached annotations.</summary>
    /// <param name="pageIndex">The edited page.</param>
    /// <param name="changed">Whether the edit succeeded.</param>
    /// <returns><paramref name="changed"/>.</returns>
    private bool Changed(int pageIndex, bool changed)
    {
        if (changed)
        {
            _ = _annotationCache.Remove(pageIndex);
            _ = Interlocked.Increment(ref _unsavedChanges);
            InvalidateLayerView();
        }

        return changed;
    }
}
