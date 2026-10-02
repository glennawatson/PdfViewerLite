// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures comment replies and review status: replying (paired with a remove), reading a comment's thread, and
/// linking replies to their comments when a file is saved. Allocations are checked from the EventPipe trace.
/// </summary>
public class ReplyBenchmarks
{
    /// <summary>The page count of the generated document.</summary>
    private const int DocumentPages = 2;

    /// <summary>The replies already in the thread that is read.</summary>
    private const int ExistingReplies = 5;

    /// <summary>The initial capacity of the save buffer.</summary>
    private const int SaveCapacity = 1 << 20;

    /// <summary>Where the comment is.</summary>
    private static readonly PagePoint NoteAt = new(100, 100);

    /// <summary>The thread read, reused.</summary>
    private readonly List<AnnotationReply> _replies = [with(ExistingReplies)];

    /// <summary>The temporary file.</summary>
    private string _path = string.Empty;

    /// <summary>The document.</summary>
    private PdfiumDocument _document = null!;

    /// <summary>The comment's index.</summary>
    private int _note;

    /// <summary>The document saved as PDFium writes it, with replies still to link.</summary>
    private byte[] _unlinked = [];

    /// <summary>Gets the marker ending each part of a saved file.</summary>
    private static ReadOnlySpan<byte> EndOfFile => "%%EOF"u8;

    /// <summary>Opens the document and adds a comment with a thread of replies.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _path = TestPdf.WriteTempFile(DocumentPages);
        _document = (PdfiumDocument)new PdfiumEngine().Open(_path, null);
        _note = _document.AddNote(0, NoteAt, "Is this right?", AnnotationColors.Sand);
        for (var i = 0; i < ExistingReplies; i++)
        {
            _ = _document.AddReply(0, _note, "Looked again; yes.", i == ExistingReplies - 1 ? ReviewState.Accepted : ReviewState.None);
        }

        _unlinked = SaveUnlinked();
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        File.Delete(_path);
    }

    /// <summary>Replies to the comment, then removes the reply.</summary>
    /// <returns>Whether it was removed.</returns>
    [Benchmark]
    public bool Reply() => _document.Remove(0, _document.AddReply(0, _note, "One more thought.", ReviewState.None));

    /// <summary>Reads the comment's thread.</summary>
    /// <returns>The reply count.</returns>
    [Benchmark]
    public int ReadThread()
    {
        _replies.Clear();
        _document.GetReplies(0, _note, _replies);
        return _replies.Count;
    }

    /// <summary>Links the replies in a saved file to their comments.</summary>
    /// <returns>The linked file's length.</returns>
    [Benchmark]
    public int LinkReplies() => AnnotationReplyLinks.Link(_unlinked).Length;

    /// <summary>Gets the file as PDFium writes it, before replies are linked, by saving and undoing the link.</summary>
    /// <returns>The bytes.</returns>
    private byte[] SaveUnlinked()
    {
        using var saved = new MemoryStream(SaveCapacity);
        _ = _document.Save(saved);
        var linked = saved.ToArray();

        // The link is an update appended after PDFium's output; the last %%EOF before it ends PDFium's part.
        var update = linked.AsSpan().LastIndexOf(EndOfFile);
        var pdfium = linked.AsSpan(0, update).LastIndexOf(EndOfFile);
        return pdfium < 0 ? linked : linked[..(pdfium + EndOfFile.Length)];
    }
}
