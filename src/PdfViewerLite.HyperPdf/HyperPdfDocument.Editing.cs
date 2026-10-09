// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Ocr;
using PdfViewerLite.Core.Text;

namespace PdfViewerLite.HyperPdf;

/// <content>
/// Annotation, text box, image signature and text layer editing, all made in the managed objects, which are the only
/// copy of the edits. Each edit drops what was read or drawn from the old objects and moves the edit version on, so
/// pages, text and links follow it. Saving writes the managed objects.
/// </content>
public sealed partial class HyperPdfDocument : IAnnotationEditor, ITextBoxEditor, IImageSignatureEditor, ITextLayerWriter
{
    /// <summary>Serialises edits and saves, so each edit and the caches it drops change together.</summary>
    private readonly Lock _editGate = new();

    /// <summary>The number of edits made since opening.</summary>
    private long _editVersion;

    /// <summary>The edit version the last successful save wrote.</summary>
    private long _savedVersion;

    /// <inheritdoc/>
    public bool HasUnsavedChanges => !IsDisposed && Volatile.Read(ref _editVersion) != Volatile.Read(ref _savedVersion);

    /// <inheritdoc/>
    public string Author
    {
        get => Annotations.Author;
        set => Annotations.Author = value;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetAnnotations(int pageIndex, List<PageAnnotation> output) => Annotations.GetAnnotations(pageIndex, output);

    /// <inheritdoc/>
    public int AddMarkup(int pageIndex, AnnotationKind kind, ReadOnlySpan<PageRect> lines, uint color, string contents)
    {
        lock (_editGate)
        {
            return Added(pageIndex, Annotations.AddMarkup(pageIndex, kind, lines, color, contents));
        }
    }

    /// <inheritdoc/>
    public int AddInk(int pageIndex, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> strokeLengths, uint color, float width, AnnotationKind kind)
    {
        lock (_editGate)
        {
            return Added(pageIndex, Annotations.AddInk(pageIndex, points, strokeLengths, color, width, kind));
        }
    }

    /// <inheritdoc/>
    public int AddNote(int pageIndex, PagePoint location, string contents, uint color)
    {
        lock (_editGate)
        {
            return Added(pageIndex, Annotations.AddNote(pageIndex, location, contents, color));
        }
    }

    /// <inheritdoc/>
    public int AddText(int pageIndex, PagePoint location, string text, float fontSize, uint color, AnnotationKind kind)
    {
        lock (_editGate)
        {
            return Added(pageIndex, Annotations.AddText(pageIndex, location, text, fontSize, color, kind));
        }
    }

    /// <inheritdoc/>
    public int AddShape(int pageIndex, AnnotationKind kind, PagePoint start, PagePoint end, uint color, float width)
    {
        lock (_editGate)
        {
            return Added(pageIndex, Annotations.AddShape(pageIndex, kind, start, end, color, width));
        }
    }

    /// <inheritdoc/>
    public int AddStamp(int pageIndex, PagePoint location, string label, uint color)
    {
        lock (_editGate)
        {
            return Added(pageIndex, Annotations.AddStamp(pageIndex, location, label, color));
        }
    }

    /// <inheritdoc/>
    public int AddImageStamp(int pageIndex, PageRect bounds, ReadOnlySpan<byte> pixels, int width, int height)
    {
        lock (_editGate)
        {
            return Added(pageIndex, Annotations.AddImageStamp(pageIndex, bounds, pixels, width, height));
        }
    }

    /// <inheritdoc/>
    public int AddPolygon(int pageIndex, AnnotationKind kind, ReadOnlySpan<PagePoint> vertices, uint color, float width)
    {
        lock (_editGate)
        {
            return Added(pageIndex, Annotations.AddPolygon(pageIndex, kind, vertices, color, width));
        }
    }

    /// <inheritdoc/>
    public int AddCallout(int pageIndex, PagePoint target, PagePoint location, string text, float fontSize, uint color)
    {
        lock (_editGate)
        {
            return Added(pageIndex, Annotations.AddCallout(pageIndex, target, location, text, fontSize, color));
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetReplies(int pageIndex, int index, List<AnnotationReply> output) => Annotations.GetReplies(pageIndex, index, output);

    /// <inheritdoc/>
    public int AddReply(int pageIndex, int index, string contents, ReviewState state)
    {
        lock (_editGate)
        {
            return Added(pageIndex, Annotations.AddReply(pageIndex, index, contents, state));
        }
    }

    /// <inheritdoc/>
    public bool SetColor(int pageIndex, int index, uint color)
    {
        lock (_editGate)
        {
            return Changed(pageIndex, Annotations.SetColor(pageIndex, index, color));
        }
    }

    /// <inheritdoc/>
    public bool SetContents(int pageIndex, int index, string contents)
    {
        lock (_editGate)
        {
            return Changed(pageIndex, Annotations.SetContents(pageIndex, index, contents));
        }
    }

    /// <inheritdoc/>
    public bool SetBounds(int pageIndex, int index, PageRect bounds)
    {
        lock (_editGate)
        {
            return Changed(pageIndex, Annotations.SetBounds(pageIndex, index, bounds));
        }
    }

    /// <inheritdoc/>
    public bool SetLineWidth(int pageIndex, int index, float width)
    {
        lock (_editGate)
        {
            return Changed(pageIndex, Annotations.SetLineWidth(pageIndex, index, width));
        }
    }

    /// <inheritdoc/>
    public bool SetFontSize(int pageIndex, int index, float fontSize)
    {
        lock (_editGate)
        {
            return Changed(pageIndex, Annotations.SetFontSize(pageIndex, index, fontSize));
        }
    }

    /// <inheritdoc/>
    public bool SetRemoved(int pageIndex, int index, bool removed)
    {
        lock (_editGate)
        {
            return Changed(pageIndex, Annotations.SetRemoved(pageIndex, index, removed));
        }
    }

    /// <inheritdoc/>
    public bool Remove(int pageIndex, int index)
    {
        lock (_editGate)
        {
            return Changed(pageIndex, Annotations.Remove(pageIndex, index));
        }
    }

    /// <inheritdoc/>
    public bool Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        lock (_editGate)
        {
            if (IsDisposed)
            {
                return false;
            }

            var version = Volatile.Read(ref _editVersion);
            if (!Annotations.Save(destination))
            {
                return false;
            }

            Volatile.Write(ref _savedVersion, version);
            return true;
        }
    }

    /// <inheritdoc/>
    public int AddTextBox(int pageIndex, PagePoint location, float wrapWidth, string text, TextFormat format)
    {
        lock (_editGate)
        {
            return Added(pageIndex, Annotations.AddTextBox(pageIndex, location, wrapWidth, text, format));
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextBoxContent? GetTextBox(int pageIndex, int index) => Annotations.GetTextBox(pageIndex, index);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetFirstBaseline(string text, TextFormat format) => Annotations.GetFirstBaseline(text, format);

    /// <inheritdoc/>
    public int AddImageSignature(int pageIndex, PageRect bounds, ReadOnlySpan<byte> pixels, int width, int height)
    {
        lock (_editGate)
        {
            return Added(pageIndex, Annotations.AddImageSignature(pageIndex, bounds, pixels, width, height));
        }
    }

    /// <inheritdoc/>
    public int AddTextLayer(int pageIndex, ReadOnlySpan<OcrWord> words)
    {
        lock (_editGate)
        {
            var written = Annotations.AddTextLayer(pageIndex, words);
            if (written > 0)
            {
                // The page has new text: its characters, words and web links are read again.
                HyperPdfLibrary.Document.PdfDocumentText.GetTextPages(_document).Clear();
                Edited();
            }

            return written;
        }
    }

    /// <summary>Records an edit that changed a form field's value and appearance.</summary>
    /// <param name="changed">Whether the edit succeeded.</param>
    /// <returns><paramref name="changed"/>.</returns>
    private bool FieldChanged(bool changed)
    {
        if (changed)
        {
            Edited();
        }

        return changed;
    }

    /// <summary>Records an edit that added an annotation or reply.</summary>
    /// <param name="pageIndex">The page edited.</param>
    /// <param name="index">The new index, or -1 when nothing was added.</param>
    /// <returns><paramref name="index"/>.</returns>
    private int Added(int pageIndex, int index)
    {
        _ = Changed(pageIndex, index >= 0);
        return index;
    }

    /// <summary>Records an edit to a page's annotations.</summary>
    /// <param name="pageIndex">The page edited.</param>
    /// <param name="changed">Whether the edit succeeded.</param>
    /// <returns><paramref name="changed"/>.</returns>
    private bool Changed(int pageIndex, bool changed)
    {
        if (changed && (uint)pageIndex < (uint)PageCount)
        {
            Edited();
        }

        return changed;
    }

    /// <summary>
    /// Drops everything read or drawn from the objects before an edit: the library's pages and links, this document's
    /// links and page pictures. Moving the edit version on marks the document unsaved.
    /// </summary>
    private void Edited()
    {
        HyperPdfLibrary.Document.PdfDocumentEditing.InvalidateCaches(_document);
        Volatile.Write(ref _links, null);
        ResetRenderer();
        _ = Interlocked.Increment(ref _editVersion);
    }
}
