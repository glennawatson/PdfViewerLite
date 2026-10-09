// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Text.Fonts;
using PdfViewerLite.Core.Text.Layout;

namespace PdfViewerLite.HyperPdf;

/// <summary>
/// Reads and edits annotations natively, with the observable behaviour of the PDFium engine: the same indexes, kinds,
/// colours, bounds, authors, dates, replies, custom keys and removed-but-kept annotations. Edits go into the document's
/// objects copy-on-write, so readers never see half an edit. Safe to call from any thread: one lock serialises the
/// editor.
/// </summary>
[DebuggerDisplay("HyperPdfAnnotations: {_unsavedChanges} unsaved changes")]
internal sealed partial class HyperPdfAnnotations : IAnnotationEditor, ITextBoxEditor, IImageSignatureEditor
{
    /// <summary>The "print" flag, so annotations appear on paper too.</summary>
    private const PdfAnnotationFlags PrintFlags = PdfAnnotationFlags.Print;

    /// <summary>Guards every read and edit.</summary>
    private readonly Lock _gate = new();

    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The document's objects.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The names the editor uses that the library does not know.</summary>
    private readonly AnnotationNames _names;

    /// <summary>The annotations of each page read so far, dropped when the page is edited.</summary>
    private readonly Dictionary<int, PageAnnotation[]> _cache = [];

    /// <summary>The pages holding an annotation removed but kept, which saving leaves out.</summary>
    private readonly HashSet<int> _pagesWithRemoved = [];

    /// <summary>The layout reused for every text box.</summary>
    private readonly TextBoxLayout _layout = new();

    /// <summary>The number of edits since opening or the last save.</summary>
    private int _unsavedChanges;

    /// <summary>The author recorded on new annotations.</summary>
    private string _author = Environment.UserName;

    /// <summary>The installed fonts, or <see langword="null"/> for the system's.</summary>
    private FontCatalog? _catalog;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfAnnotations"/> class.</summary>
    /// <param name="document">The document.</param>
    internal HyperPdfAnnotations(PdfDocument document)
    {
        _document = document;
        _store = document.Objects;
        _names = AnnotationNames.Create(_store.Names);
    }

    /// <inheritdoc/>
    public bool HasUnsavedChanges => Volatile.Read(ref _unsavedChanges) != 0;

    /// <inheritdoc/>
    public string Author
    {
        get => Volatile.Read(ref _author);
        set => Volatile.Write(ref _author, string.IsNullOrWhiteSpace(value) ? Environment.UserName : value.Trim());
    }

    /// <summary>Gets or sets the clock used for modification dates; tests replace it.</summary>
    internal static TimeProvider Clock { get; set; } = TimeProvider.System;

    /// <summary>Gets or sets the installed fonts text boxes may be written in; the computer's fonts unless set.</summary>
    internal FontCatalog FontCatalog
    {
        get
        {
            lock (_gate)
            {
                return _catalog ?? FontCatalog.System;
            }
        }

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (_gate)
            {
                _catalog = value;
            }
        }
    }

    /// <inheritdoc/>
    public void GetAnnotations(int pageIndex, List<PageAnnotation> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        lock (_gate)
        {
            if (_cache.TryGetValue(pageIndex, out var cached))
            {
                output.AddRange(cached);
                return;
            }

            if (GetPage(pageIndex) is not { } page)
            {
                return;
            }

            var start = output.Count;
            Read(page, output);
            _cache[pageIndex] = [.. CollectionsMarshal.AsSpan(output)[start..]];
        }
    }

    /// <inheritdoc/>
    public bool SetContents(int pageIndex, int index, string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        lock (_gate)
        {
            if (!TryEdit(pageIndex, index, out var page, out var annotation))
            {
                return false;
            }

            PdfAnnotations.SetText(annotation, KnownName.Contents, contents);
            SetModified(annotation);
            return Commit(pageIndex, page, index, annotation);
        }
    }

    /// <inheritdoc/>
    public bool SetRemoved(int pageIndex, int index, bool removed)
    {
        lock (_gate)
        {
            if (!TryEdit(pageIndex, index, out var page, out var annotation) || IsRemoved(annotation) == removed)
            {
                return false;
            }

            var flags = PdfAnnotations.GetFlags(annotation);
            if (removed)
            {
                PdfAnnotations.SetNumberText(annotation, _names.Removed, (int)flags);
                PdfAnnotations.SetFlags(annotation, flags | PdfAnnotationFlags.Hidden);
                _ = _pagesWithRemoved.Add(pageIndex);
            }
            else
            {
                PdfAnnotations.SetFlags(annotation, (PdfAnnotationFlags)(int)PdfAnnotations.GetNumberText(annotation, _names.Removed));
                _ = annotation.Remove(_names.Removed);
            }

            return Commit(pageIndex, page, index, annotation);
        }
    }

    /// <inheritdoc/>
    public bool Remove(int pageIndex, int index)
    {
        lock (_gate)
        {
            return GetPage(pageIndex) is { } page && Changed(pageIndex, PdfPageAnnotations.RemoveAt(_store, page, index));
        }
    }

    /// <summary>Converts a page point to user space.</summary>
    /// <param name="page">The page.</param>
    /// <param name="point">The point in page space.</param>
    /// <returns>The point in user space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector2 ToUser(PdfPage page, PagePoint point) => page.ToUser(new(point.X, point.Y));

    /// <summary>Converts a user space rectangle to page space.</summary>
    /// <param name="page">The page.</param>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns>The page rectangle.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PageRect ToPageRect(PdfPage page, PdfRectangle rectangle) => LinkTargets.ToPageRect(page.ToViewerRectangle(rectangle));

    /// <summary>Converts a page rectangle to user space.</summary>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The rectangle in page space.</param>
    /// <returns>The user space rectangle.</returns>
    private static PdfRectangle ToUserRectangle(PdfPage page, PageRect bounds)
    {
        var a = ToUser(page, new(bounds.Left, bounds.Top));
        var b = ToUser(page, new(bounds.Right, bounds.Bottom));
        return PdfRectangle.FromCorners(a.X, a.Y, b.X, b.Y);
    }

    /// <summary>Records the current time as the modification date.</summary>
    /// <param name="annotation">The annotation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetModified(PdfDictionary annotation) => PdfAnnotations.SetDate(annotation, KnownName.M, Clock.GetUtcNow());

    /// <summary>Gets a page, or <see langword="null"/> when the index is out of range or the document is closed.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The page.</returns>
    private PdfPage? GetPage(int pageIndex) =>
        _document.IsDisposed || (uint)pageIndex >= (uint)_document.PageCount ? null : PdfDocumentPages.GetPage(_document, pageIndex);

    /// <summary>Appends the annotations on a page, skipping links, form fields, pop-ups, replies and removed ones.</summary>
    /// <param name="page">The page.</param>
    /// <param name="output">The list.</param>
    private void Read(PdfPage page, List<PageAnnotation> output)
    {
        if (PdfPageAnnotations.GetArray(_store, page) is not { } annotations)
        {
            return;
        }

        for (var i = 0; i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is not { } annotation || GetKind(annotation) is not { } kind || IsRemoved(annotation) || IsReply(annotation))
            {
                continue;
            }

            var bounds = ToPageRect(page, PdfAnnotations.GetRectangle(annotation));
            output.Add(new(page.Index, i, kind, bounds, GetColor(annotation, kind), PdfAnnotations.GetText(annotation, KnownName.Contents), PdfAnnotations.GetText(annotation, KnownName.T))
            {
                LineWidth = PdfAnnotations.GetBorderWidth(annotation),
                FontSize = PdfAnnotations.GetNumberText(annotation, _names.FontSize),
                Modified = PdfAnnotations.GetDate(annotation, KnownName.M),
            });
        }
    }

    /// <summary>Copies an annotation for editing.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="page">The page.</param>
    /// <param name="annotation">The copy to edit.</param>
    /// <returns><see langword="true"/> when the annotation exists.</returns>
    private bool TryEdit(int pageIndex, int index, out PdfPage page, out PdfDictionary annotation)
    {
        page = GetPage(pageIndex)!;
        annotation = page is null ? null! : PdfPageAnnotations.Get(_store, page, index)?.Clone()!;
        return annotation is not null;
    }

    /// <summary>Puts an edited annotation back and records the edit.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="annotation">The edited annotation.</param>
    /// <returns><see langword="true"/> when put back.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Commit(int pageIndex, PdfPage page, int index, PdfDictionary annotation) =>
        Changed(pageIndex, PdfPageAnnotations.Replace(_store, page, index, annotation));

    /// <summary>Fills in what every new annotation records, then adds it to the page.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="annotation">The annotation, with its rectangle set.</param>
    /// <param name="color">The colour, or <see langword="null"/> for an appearance that keeps its own colours, such as a picture.</param>
    /// <param name="contents">The note text, or empty.</param>
    /// <param name="subject">The <c>/Subj</c>, or empty.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    private int Add(int pageIndex, PdfPage page, PdfDictionary annotation, uint? color, string contents, ReadOnlySpan<byte> subject)
    {
        Finish(annotation, color, contents, subject);
        return Changed(pageIndex, PdfPageAnnotations.Append(_store, page, annotation));
    }

    /// <summary>Sets the colour, opacity, flags, author, date, note text and subject of a new annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="color">The colour, or <see langword="null"/> for none.</param>
    /// <param name="contents">The note text, or empty.</param>
    /// <param name="subject">The <c>/Subj</c>, or empty.</param>
    private void Finish(PdfDictionary annotation, uint? color, string contents, ReadOnlySpan<byte> subject)
    {
        if (color is { } rgb)
        {
            PdfAnnotations.SetColor(annotation, KnownName.C, rgb);
        }

        PdfAnnotations.SetOpacity(annotation, 1);
        PdfAnnotations.SetFlags(annotation, PrintFlags);
        PdfAnnotations.SetText(annotation, KnownName.T, Author);
        SetModified(annotation);
        if (contents.Length > 0)
        {
            PdfAnnotations.SetText(annotation, KnownName.Contents, contents);
        }

        if (!subject.IsEmpty)
        {
            annotation.Set(_names.Subject, PdfValue.FromString(subject.ToArray()));
        }
    }

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
            _ = _cache.Remove(pageIndex);
            _ = Interlocked.Increment(ref _unsavedChanges);
        }

        return changed;
    }
}
