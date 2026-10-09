// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>
/// A page's content stream read into objects (text, paths, images, shadings and forms) that can be inspected, deleted,
/// moved and recoloured. <see cref="Regenerate()"/> writes the content again: objects nobody changed keep their original
/// bytes, so marked content (<c>/MCID</c>) and the structure tree links stay as they were. A content object is not thread
/// safe; read one per thread.
/// </summary>
[DebuggerDisplay("PdfPageContent: {Objects.Count} objects")]
public sealed partial class PdfPageContent
{
    /// <summary>The nesting of forms read into objects.</summary>
    private const int MaxFormDepth = 16;

    /// <summary>The content as it was read.</summary>
    private readonly byte[] _source;

    /// <summary>The objects, in painting order.</summary>
    private readonly List<PdfPageObject> _objects = [];

    /// <summary>The content added after the regenerated content.</summary>
    private readonly List<byte> _appended = [];

    /// <summary>The resources the regenerated content names that are stored when it is applied.</summary>
    private readonly List<PendingResource> _pending = [];

    /// <summary>How deep this content is nested in forms.</summary>
    private readonly int _depth;

    /// <summary>The restores that had no matching save.</summary>
    private int _underflow;

    /// <summary>The saves left open at the end.</summary>
    private int _unclosed;

    /// <summary>Initializes a new instance of the <see cref="PdfPageContent"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page, or <see langword="null"/> for a form's content.</param>
    /// <param name="form">The form the content belongs to, or <see langword="null"/> for a page's.</param>
    /// <param name="resources">The resources the content names.</param>
    /// <param name="baseMatrix">The matrix from the content's space to user space.</param>
    /// <param name="source">The decoded content.</param>
    /// <param name="depth">How deep the content is nested in forms.</param>
    private PdfPageContent(PdfDocument document, PdfPage? page, PdfFormObject? form, PdfDictionary? resources, Matrix3x2 baseMatrix, byte[] source, int depth)
    {
        Document = document;
        Page = page;
        FormObject = form;
        Resources = resources;
        BaseMatrix = baseMatrix;
        _source = source;
        _depth = depth;
    }

    /// <summary>Gets the document.</summary>
    public PdfDocument Document { get; }

    /// <summary>Gets the page, or <see langword="null"/> for the content of a form.</summary>
    public PdfPage? Page { get; }

    /// <summary>Gets the resources the content names; those a form inherits from the content that paints it when it has none.</summary>
    public PdfDictionary? Resources { get; }

    /// <summary>Gets the matrix from the content's space to user space: the identity for a page.</summary>
    public Matrix3x2 BaseMatrix { get; }

    /// <summary>Gets the objects, in painting order.</summary>
    public IReadOnlyList<PdfPageObject> Objects => _objects;

    /// <summary>Gets the decoded content as it was read.</summary>
    public ReadOnlySpan<byte> Source => _source;

    /// <summary>Gets a value indicating whether any object changed or content was added.</summary>
    public bool IsModified => _appended.Count > 0 || _pending.Count > 0 || AnyObjectModified() || AnyMarkScrubbed();

    /// <summary>Gets the form object this content belongs to, or <see langword="null"/> for a page.</summary>
    internal PdfFormObject? FormObject { get; }

    /// <summary>Gets the marked-content sequences, in the order their opening operators appear.</summary>
    internal List<PdfMark> Begins { get; } = [];

    /// <summary>Reads a page's content into objects.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfPageContent Read(PdfDocument document, int pageIndex) => Read(document, pageIndex, CancellationToken.None);

    /// <summary>Reads a page's content into objects.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels the read before it starts.</param>
    /// <returns>The content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<PdfPageContent> ReadAsync(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(Read(document, pageIndex, cancellationToken));
    }

    /// <summary>Adds bytes to the end of the regenerated content, in a graphics state that is balanced and unclipped by the content before.</summary>
    /// <param name="content">The operators to add.</param>
    public void Append(ReadOnlySpan<byte> content)
    {
        _appended.AddRange(content);
        _appended.Add((byte)'\n');
    }

    /// <summary>Makes a resource name that is free in the content's resources and in what has been added.</summary>
    /// <param name="category">The resource category.</param>
    /// <param name="prefix">The start of the name.</param>
    /// <returns>The name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="prefix"/> is <see langword="null"/>.</exception>
    public PdfName AllocateName(KnownName category, string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        var names = Document.Objects.Names;
        var existing = Resources?.GetDictionary(category);
        var number = 1;
        while (true)
        {
            var candidate = names.Intern(string.Create(CultureInfo.InvariantCulture, $"{prefix}{number}"));
            if ((existing is null || !existing.ContainsKey(candidate)) && !IsPending(category, candidate))
            {
                return candidate;
            }

            number++;
        }
    }

    /// <summary>Adds a resource the appended content names. It is stored in the resources when the content is applied.</summary>
    /// <param name="category">The resource category, such as /Font or /XObject.</param>
    /// <param name="name">The name from <see cref="AllocateName"/>.</param>
    /// <param name="value">A stream or dictionary to store, or a reference.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddResource(KnownName category, PdfName name, PdfValue value) => _pending.Add(new(category, name, value, null));

    /// <summary>Reads a page's content into objects, checking a token while it walks the operators.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static PdfPageContent Read(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        var page = document.GetPage(pageIndex);

        // The page object may predate edits made in the open transaction, so the current dictionary is the authority.
        var current = PdfPageAnnotations.GetPageDictionary(document.Objects, page);
        var buffer = default(PooledBuffer);
        try
        {
            ContentInterpreter.DecodeContents(current.Get(KnownName.Contents), ref buffer);
            var content = new PdfPageContent(document, page, null, current.GetDictionary(KnownName.Resources) ?? page.Resources, Matrix3x2.Identity, buffer.ToArray(), 0);
            content.Load(null, cancellationToken);
            return content;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Reads the content of a form object into objects whose matrices include the form's matrix.</summary>
    /// <param name="owner">The content that paints the form.</param>
    /// <param name="form">The form object.</param>
    /// <returns>The form's content.</returns>
    /// <exception cref="InvalidOperationException">The forms nest too deeply.</exception>
    internal static PdfPageContent ReadForm(PdfPageContent owner, PdfFormObject form)
    {
        if (owner._depth >= MaxFormDepth)
        {
            throw new InvalidOperationException("The forms nest too deeply.");
        }

        var resources = form.Stream.Dictionary.GetDictionary(KnownName.Resources) ?? owner.Resources;
        var start = form.FormMatrix * form.Matrix;
        var content = new PdfPageContent(owner.Document, null, form, resources, start, form.Stream.DecodeToArray(), owner._depth + 1);
        content.Load(FormClip(form, start), CancellationToken.None);
        return content;
    }

    /// <summary>Gets the clip a form's bounding box puts on its content, inside the clip it is painted with.</summary>
    /// <param name="form">The form object.</param>
    /// <param name="start">The matrix from the form's space to user space.</param>
    /// <returns>The innermost clip, or <see langword="null"/> when there is none.</returns>
    private static ClipNode? FormClip(PdfFormObject form, Matrix3x2 start)
    {
        var parent = form.ClipChain;
        if (form.BoundingBox is not { } box)
        {
            return parent;
        }

        PdfPathSegment[] outline = [new(PdfPathSegmentKind.Rectangle, box.Left, box.Bottom, box.Width, box.Height, 0, 0)];
        var bounds = PathBounds.Measure(outline, start, 0);
        var clipped = parent is null ? bounds : parent.Bounds.Intersect(bounds);
        return new(parent, new(outline, start, false, bounds), clipped);
    }

    /// <summary>Determines whether any object has a change to write.</summary>
    /// <returns><see langword="true"/> when one does.</returns>
    private bool AnyObjectModified()
    {
        foreach (var item in _objects)
        {
            if (item.IsModified)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether any marked-content sequence was scrubbed.</summary>
    /// <returns><see langword="true"/> when one was.</returns>
    private bool AnyMarkScrubbed()
    {
        foreach (var mark in Begins)
        {
            if (mark.IsScrubbed)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a name is used by a resource that is waiting to be stored.</summary>
    /// <param name="category">The category.</param>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when the name is taken.</returns>
    private bool IsPending(KnownName category, PdfName name)
    {
        foreach (var resource in _pending)
        {
            if (resource.Category == category && resource.Name == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Parses the content into objects.</summary>
    /// <param name="clip">The clip the content starts inside, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    private void Load(ClipNode? clip, CancellationToken cancellationToken)
    {
        var parser = new PageContentParser(this, _source, Resources, BaseMatrix, clip);
        _objects.AddRange(parser.Parse(cancellationToken));
        _underflow = parser.Underflow;
        _unclosed = parser.Unclosed;
    }
}
