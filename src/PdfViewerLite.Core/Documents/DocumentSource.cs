// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.Core.Documents;

/// <summary>
/// A document a tab refers to. The native document is opened on demand through its <see cref="DocumentPool"/> and may
/// be closed again when the pool needs room; page sizes, outline and metadata are kept so a closed document can still
/// be laid out and navigated.
/// </summary>
[DebuggerDisplay("DocumentSource: {FilePath} (open: {IsOpen})")]
public sealed class DocumentSource
{
    /// <summary>The last identifier handed out.</summary>
    private static int _lastId;

    /// <summary>The owning pool.</summary>
    private readonly DocumentPool _pool;

    /// <summary>The open document, if any.</summary>
    private IDocument? _document;

    /// <summary>Initializes a new instance of the <see cref="DocumentSource"/> class.</summary>
    /// <param name="pool">The owning pool.</param>
    /// <param name="filePath">The file path.</param>
    /// <param name="password">The password.</param>
    internal DocumentSource(DocumentPool pool, string filePath, string? password)
    {
        _pool = pool;
        FilePath = filePath;
        Password = password;
        Id = Interlocked.Increment(ref _lastId);
    }

    /// <summary>Gets an identifier that changes whenever the file is reloaded, used to key cached tiles.</summary>
    public int Id { get; private set; }

    /// <summary>Gets the file path.</summary>
    public string FilePath { get; }

    /// <summary>Gets or sets the password used to open the document.</summary>
    public string? Password { get; set; }

    /// <summary>Gets a value indicating whether the native document is open.</summary>
    public bool IsOpen => _document is { IsDisposed: false };

    /// <summary>Gets a value indicating whether the open document has edits that are not saved; such documents are never closed to save memory.</summary>
    public bool HasUnsavedChanges => ((_document)?.GetFeature(typeof(IAnnotationEditor)) as IAnnotationEditor) is { HasUnsavedChanges: true } && IsOpen;

    /// <summary>Gets the page sizes, or an empty array before the first open.</summary>
    public PageSize[] PageSizes { get; private set; } = [];

    /// <summary>Gets the number of pages, or zero before the first open.</summary>
    public int PageCount => PageSizes.Length;

    /// <summary>Gets the cached outline, or <see langword="null"/> before the first open.</summary>
    public IReadOnlyList<OutlineNode>? Outline { get; private set; }

    /// <summary>Gets the cached metadata, or <see langword="null"/> before the first open.</summary>
    public DocumentMetadata? Metadata { get; private set; }

    /// <summary>Gets the time the document was last acquired, in <see cref="Environment.TickCount64"/> units.</summary>
    public long LastUsed { get; private set; }

    /// <summary>Gets the open document, opening it when necessary.</summary>
    /// <returns>The document.</returns>
    /// <exception cref="DocumentOpenException">Thrown when the document cannot be opened.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IDocument Acquire() => _pool.Acquire(this);

    /// <summary>Closes the document and forgets cached information so the next acquire reads the file again.</summary>
    public void Reload()
    {
        Close();
        PageSizes = [];
        Outline = null;
        Metadata = null;
        Id = Interlocked.Increment(ref _lastId);
    }

    /// <summary>Refreshes page sizes and navigation after editing the open document, preserving its unsaved changes.</summary>
    public void RefreshStructure()
    {
        var document = Acquire();
        PageSizes = document.GetPageSizes();
        Outline = document.GetOutline();
        Metadata = document.GetMetadata();
        Id = Interlocked.Increment(ref _lastId);
    }

    /// <summary>Opens the native document when it is not already open.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>The document.</returns>
    internal IDocument OpenIfNeeded(IDocumentEngine engine)
    {
        LastUsed = Environment.TickCount64;
        if (_document is { IsDisposed: false } open)
        {
            return open;
        }

        var document = engine.Open(FilePath, Password);
        _document = document;
        if (PageSizes.Length == 0)
        {
            PageSizes = document.GetPageSizes();
            Metadata = document.GetMetadata();
            Outline = document.GetOutline();
        }

        return document;
    }

    /// <summary>Closes the native document, keeping cached information.</summary>
    internal void Close()
    {
        var document = _document;
        _document = null;
        document?.Dispose();
    }
}
