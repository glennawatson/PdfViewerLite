// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.IO;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Structure;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Objects;

/// <content>Incremental-update revisions, for signature checks.</content>
public sealed partial class PdfObjectStore
{
    /// <summary>The length of the <c>%%EOF</c> marker.</summary>
    private const int EndMarkerLength = 5;

    /// <summary>The bytes read at a section's start to find its first token.</summary>
    private const int SectionTokenWindow = 1024;

    /// <summary>Gets one more than the highest object number in the cross-reference table.</summary>
    internal int XrefSize => _xref.Size;

    /// <summary>Gets the end-of-file marker that closes each revision.</summary>
    private static ReadOnlySpan<byte> EndMarker => "%%EOF"u8;

    /// <summary>
    /// Gets where an object's current definition starts in the file: its own offset, or its object stream's offset when it
    /// is compressed.
    /// </summary>
    /// <param name="number">The object number.</param>
    /// <returns>The position in the file, or -1 when the object is free or missing.</returns>
    internal long GetDefinitionOffset(int number)
    {
        var xref = _xref;
        var type = xref.GetType(number);
        if (type == XrefEntryType.Compressed)
        {
            number = (int)xref.GetLocation(number);
            type = xref.GetType(number);
        }

        return type == XrefEntryType.InFile ? xref.GetLocation(number) + xref.OffsetBase : -1;
    }

    /// <summary>
    /// Opens the objects of an earlier revision: the file cut at <paramref name="length"/>. The copy shares this store's
    /// security handler, so it must not be disposed; it owns nothing else.
    /// </summary>
    /// <param name="length">The length of the earlier revision.</param>
    /// <returns>The earlier revision's objects.</returns>
    /// <exception cref="PdfException">The earlier revision cannot be read.</exception>
    internal PdfObjectStore OpenRevision(long length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, _source.Length);

        // A view of the file's start, read through this store's source, so the revision is not copied.
        var store = new PdfObjectStore(new PrefixPdfByteSource(_source, length), true, new XrefTable())
        {
            HeaderOffset = HeaderOffset,
            Version = Version,

            // The key is known already; setting it first makes every object parse decrypted.
            Security = Security,
        };
        store.ReadStructure();
        store.Catalog = store.Trailer.GetDictionary(KnownName.Root) ?? throw new PdfException(PdfError.Format, "The earlier revision has no catalog.");
        return store;
    }

    /// <summary>
    /// Lists the file's revisions, oldest first, by following the cross-reference chain from the newest section. Each
    /// revision ends at the first <c>%%EOF</c> after its section. A damaged chain falls back to every <c>%%EOF</c> marker.
    /// </summary>
    /// <returns>The revisions.</returns>
    internal PdfRevision[] ReadRevisions()
    {
        var sections = ReadSectionOffsets();
        var revisions = new List<PdfRevision>(sections.Count);
        if (sections.Count > 0)
        {
            sections.Sort();
            foreach (var offset in sections)
            {
                AddRevision(revisions, offset);
            }
        }

        return revisions.Count > 0 ? [.. revisions] : ReadMarkerRevisions();
    }

    /// <summary>Gets the end of the line that follows a position: one CR, LF or CRLF.</summary>
    /// <param name="position">The position after a marker.</param>
    /// <returns>The position after the line end.</returns>
    private long SkipLineEnd(long position)
    {
        if (_source.ByteAt(position) == '\r')
        {
            position++;
        }

        if (_source.ByteAt(position) == '\n')
        {
            position++;
        }

        return position;
    }

    /// <summary>Adds the revision whose cross-reference section starts at an offset, unless it ends where the last one did.</summary>
    /// <param name="revisions">The revisions so far.</param>
    /// <param name="offset">The section's position.</param>
    private void AddRevision(List<PdfRevision> revisions, long offset)
    {
        var length = _source.Length;
        var marker = PdfByteSearch.IndexOf(_source, EndMarker, Math.Clamp(offset, 0, length));
        var markerEnd = marker < 0 ? length : marker + EndMarkerLength;
        var end = marker < 0 ? length : SkipLineEnd(markerEnd);
        if (revisions.Count > 0 && revisions[^1].MarkerEnd >= markerEnd)
        {
            return;
        }

        revisions.Add(new(revisions.Count, offset, markerEnd, end));
    }

    /// <summary>Lists a revision for every <c>%%EOF</c> marker, for files whose chain cannot be followed.</summary>
    /// <returns>The revisions.</returns>
    private PdfRevision[] ReadMarkerRevisions()
    {
        var revisions = new List<PdfRevision>();
        var position = 0L;
        while (revisions.Count < PdfLimits.MaxXrefSections)
        {
            var marker = PdfByteSearch.IndexOf(_source, EndMarker, position);
            if (marker < 0)
            {
                break;
            }

            var markerEnd = marker + EndMarkerLength;
            revisions.Add(new(revisions.Count, -1, markerEnd, SkipLineEnd(markerEnd)));
            position = markerEnd;
        }

        return revisions.Count > 0 ? [.. revisions] : [new(0, -1, _source.Length, _source.Length)];
    }

    /// <summary>Follows /Prev from the newest cross-reference section, collecting each section's position.</summary>
    /// <returns>The positions, newest first; empty when the chain cannot be followed.</returns>
    private List<long> ReadSectionOffsets()
    {
        var sections = new List<long>();
        var offset = StartXref;
        var visited = new HashSet<long>();
        while (offset >= 0 && sections.Count < PdfLimits.MaxXrefSections)
        {
            var position = offset + _xref.OffsetBase;
            if (!visited.Add(position) || position >= _source.Length || ReadSectionTrailer(position) is not { } trailer)
            {
                break;
            }

            sections.Add(position);
            var previous = trailer.GetRaw(KnownName.Prev);
            offset = previous.Kind == PdfKind.Integer ? previous.AsInteger() : -1;
        }

        return sections;
    }

    /// <summary>Reads the trailer of the cross-reference section at a position: a classic table's trailer or a stream's dictionary.</summary>
    /// <param name="position">The section's position.</param>
    /// <returns>The trailer, or <see langword="null"/> when no section starts there.</returns>
    private PdfDictionary? ReadSectionTrailer(long position)
    {
        PdfTokenKind kind;
        bool isTable;
        long afterKeyword;
        using (var window = _source.Lease(position, (int)Math.Min(SectionTokenWindow, _source.Length - position)))
        {
            var lexer = new PdfLexer(window.Span);
            kind = lexer.Next();
            isTable = kind == PdfTokenKind.Keyword && lexer.Keyword == PdfKeyword.Xref;
            afterKeyword = position + lexer.Position;
        }

        if (isTable)
        {
            var trailer = PdfByteSearch.IndexOf(_source, PdfKeywords.Trailer, afterKeyword);
            return trailer < 0 ? null : ParseValueAt(trailer + PdfKeywords.Trailer.Length).AsDictionary();
        }

        return kind == PdfTokenKind.Number && TryParseObjectAt(position, out _, out var value) ? value.AsDictionary() : null;
    }
}
