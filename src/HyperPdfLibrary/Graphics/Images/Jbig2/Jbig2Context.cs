// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// Reads a sequence of JBIG2 segments (T.88 annex D, as embedded in PDF) and renders them onto a page bitmap the size
/// of the PDF image. Segments are processed in order until the end of the data, an end-of-page or end-of-file
/// segment, or damage, following PDFium's segment handling. A context for /JBIG2Globals holds global segments that
/// page segments may refer to.
/// </summary>
[DebuggerDisplay("Jbig2Context: {_segments.Count} segments")]
internal sealed partial class Jbig2Context : IDisposable
{
    /// <summary>The smallest segment header; fewer bytes left end processing.</summary>
    private const int MinSegmentSize = 11;

    /// <summary>The bytes after an arithmetic-coded segment's data that the decoder may have read into.</summary>
    private const int ArithmeticTrailer = 2;

    /// <summary>The bytes of the row count after a generic region of unknown length.</summary>
    private const int RowCountBytes = 4;

    /// <summary>The page information bytes after the width and height: the resolutions, flags and striping.</summary>
    private const int PageInfoTail = 11;

    /// <summary>The page information flag of the default pixel value.</summary>
    private const int DefaultPixelFlag = 0x04;

    /// <summary>The offset of the page flags after the width and height.</summary>
    private const int PageFlagsOffset = 8;

    /// <summary>The highest segment type number.</summary>
    private const int TypeMask = 0x3F;

    /// <summary>The segments processed so far.</summary>
    private readonly List<Jbig2Segment> _segments = [];

    /// <summary>The page bitmap, or <see langword="null"/> for the global context.</summary>
    private readonly Jbig2Bitmap? _page;

    /// <summary>The global context, or <see langword="null"/>.</summary>
    private readonly Jbig2Context? _globals;

    /// <summary>The work budget shared with the global context.</summary>
    private readonly Jbig2Workspace _workspace;

    /// <summary>Whether a page information segment has started the page.</summary>
    private bool _inPage;

    /// <summary>Initializes a new instance of the <see cref="Jbig2Context"/> class.</summary>
    /// <param name="page">The page bitmap, or <see langword="null"/> for global segments.</param>
    /// <param name="workspace">The work budget and scratch bitmaps.</param>
    /// <param name="globals">The global context, or <see langword="null"/>.</param>
    internal Jbig2Context(Jbig2Bitmap? page, Jbig2Workspace workspace, Jbig2Context? globals)
    {
        _page = page;
        _workspace = workspace;
        _globals = globals;
    }

    /// <summary>The broad kinds of segment, as dispatched.</summary>
    private enum SegmentKind
    {
        /// <summary>Segments whose data is skipped.</summary>
        Ignored = 0,

        /// <summary>A symbol dictionary.</summary>
        SymbolDictionary = 1,

        /// <summary>A pattern dictionary.</summary>
        PatternDictionary = 2,

        /// <summary>A Huffman table.</summary>
        Table = 3,

        /// <summary>The page information.</summary>
        PageInformation = 4,

        /// <summary>An end-of-page or end-of-file segment.</summary>
        End = 5,

        /// <summary>A text region.</summary>
        TextRegion = 6,

        /// <summary>A halftone region.</summary>
        HalftoneRegion = 7,

        /// <summary>A generic region.</summary>
        GenericRegion = 8,

        /// <summary>A refinement region.</summary>
        RefinementRegion = 9,
    }

    /// <summary>Gets a value indicating whether a page information segment was processed.</summary>
    internal bool PageSeen { get; private set; }

    /// <summary>Gets the kind of each segment type number from 0 to 63.</summary>
    private static ReadOnlySpan<byte> SegmentKinds =>
    [
        0x01, 0x00, 0x00, 0x00, 0x06, 0x00, 0x06, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x02, 0x00, 0x00, 0x00, 0x07, 0x00, 0x07, 0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x08, 0x00, 0x08, 0x08, 0x09, 0x00, 0x09, 0x09, 0x00, 0x00, 0x00, 0x00,
        0x04, 0x05, 0x00, 0x05, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>Returns the buffers of every segment's results.</summary>
    public void Dispose()
    {
        foreach (var segment in _segments)
        {
            segment.Dispose();
        }

        _segments.Clear();
    }

    /// <summary>Processes segments until the end of the data, an end segment or damage.</summary>
    /// <param name="data">The segments.</param>
    /// <returns><see cref="Jbig2Status.Success"/>, or <see cref="Jbig2Status.Failure"/> on damage or empty data.</returns>
    internal Jbig2Status DecodeSequential(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            return Jbig2Status.Failure;
        }

        var reader = new Jbig2Reader(data);
        while (reader.BytesLeft >= MinSegmentSize)
        {
            PdfCancellation.ThrowIfCancelled();
            if (Jbig2Segment.Parse(ref reader) is not { } segment)
            {
                return Jbig2Status.Failure;
            }

            var status = DecodeSegment(ref reader, segment);
            if (status != Jbig2Status.Success || !MoveToNextSegment(ref reader, segment))
            {
                segment.Dispose();
                return status == Jbig2Status.EndReached ? Jbig2Status.Success : Jbig2Status.Failure;
            }

            _segments.Add(segment);
        }

        return Jbig2Status.Success;
    }

    /// <summary>Moves the reader to the next segment header.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="segment">The segment just processed.</param>
    /// <returns><see langword="false"/> when the data length overflows.</returns>
    private static bool MoveToNextSegment(ref Jbig2Reader reader, Jbig2Segment segment)
    {
        if (segment.DataLength == Jbig2Segment.UnknownLength)
        {
            reader.Skip(RowCountBytes);
            return true;
        }

        var next = (long)segment.DataOffset + segment.DataLength;
        if (next > uint.MaxValue)
        {
            return false;
        }

        reader.Offset = (int)Math.Min(next, reader.Data.Length);
        return true;
    }

    /// <summary>Finishes an arithmetic-coded segment: moves the reader to where the decoder stopped, aligned.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="position">The decoder's position.</param>
    private static void FinishArithmetic(ref Jbig2Reader reader, int position)
    {
        reader.Offset = position;
        reader.AlignByte();
        reader.Skip(ArithmeticTrailer);
    }

    /// <summary>Skips the data of a segment that needs no processing.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="segment">The segment.</param>
    /// <returns><see cref="Jbig2Status.Success"/>.</returns>
    private static Jbig2Status Skip(ref Jbig2Reader reader, Jbig2Segment segment)
    {
        reader.Skip(segment.DataLength);
        return Jbig2Status.Success;
    }

    /// <summary>Processes one segment's data.</summary>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <returns>The status.</returns>
    private Jbig2Status DecodeSegment(ref Jbig2Reader reader, Jbig2Segment segment)
    {
        var kind = (SegmentKind)SegmentKinds[(int)segment.Type & TypeMask];
        if (kind >= SegmentKind.TextRegion)
        {
            return _inPage && _page is not null ? DecodeRegion(ref reader, segment, kind, _page) : Jbig2Status.Failure;
        }

        return kind switch
        {
            SegmentKind.SymbolDictionary => DecodeSymbolDictionary(ref reader, segment),
            SegmentKind.PatternDictionary => DecodePatternDictionary(ref reader, segment),
            SegmentKind.Table => DecodeTable(ref reader, segment),
            SegmentKind.PageInformation => DecodePageInformation(ref reader),
            SegmentKind.End => End(segment),
            _ => Skip(ref reader, segment),
        };
    }

    /// <summary>Processes a region segment.</summary>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="kind">The region kind.</param>
    /// <param name="page">The page.</param>
    /// <returns>The status.</returns>
    private Jbig2Status DecodeRegion(ref Jbig2Reader reader, Jbig2Segment segment, SegmentKind kind, Jbig2Bitmap page) => kind switch
    {
        SegmentKind.TextRegion => DecodeTextRegion(ref reader, segment, page),
        SegmentKind.HalftoneRegion => DecodeHalftoneRegion(ref reader, segment, page),
        SegmentKind.GenericRegion => DecodeGenericRegion(ref reader, segment, page),
        _ => DecodeRefinementRegion(ref reader, segment, page),
    };

    /// <summary>Handles an end-of-page or end-of-file segment.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns><see cref="Jbig2Status.EndReached"/>.</returns>
    private Jbig2Status End(Jbig2Segment segment)
    {
        if (segment.Type == Jbig2SegmentType.EndOfPage)
        {
            _inPage = false;
        }

        return Jbig2Status.EndReached;
    }

    /// <summary>Processes a page information segment: the page is filled with its default pixel.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The status.</returns>
    private Jbig2Status DecodePageInformation(ref Jbig2Reader reader)
    {
        if (!reader.TryReadUInt32(out _) || !reader.TryReadUInt32(out _) || reader.BytesLeft < PageInfoTail)
        {
            return Jbig2Status.Failure;
        }

        var flags = reader.Data[reader.Offset + PageFlagsOffset];
        reader.Skip(PageInfoTail);

        // The page has the PDF image's size, as in PDFium; the page information's own size is not used.
        _page?.Fill((flags & DefaultPixelFlag) != 0);
        _inPage = true;
        PageSeen = true;
        return Jbig2Status.Success;
    }

    /// <summary>Finds a segment by number, in the global segments first.</summary>
    /// <param name="number">The segment number.</param>
    /// <returns>The segment, or <see langword="null"/>.</returns>
    private Jbig2Segment? Find(uint number)
    {
        if (_globals?.Find(number) is { } global)
        {
            return global;
        }

        foreach (var segment in _segments)
        {
            if (segment.Number == number)
            {
                return segment;
            }
        }

        return null;
    }

    /// <summary>Determines whether every segment a segment refers to has been processed.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns><see langword="true"/> when all are found.</returns>
    private bool AllReferredFound(Jbig2Segment segment)
    {
        foreach (var number in segment.Referred)
        {
            if (Find(number) is null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets the nth Huffman table segment a segment refers to.</summary>
    /// <param name="segment">The segment.</param>
    /// <param name="index">The table's position among the referred table segments.</param>
    /// <returns>The table, or <see langword="null"/>.</returns>
    private Jbig2HuffmanTable? FindReferredTable(Jbig2Segment segment, int index)
    {
        var count = 0;
        foreach (var number in segment.Referred)
        {
            if (Find(number) is not { Type: Jbig2SegmentType.Tables } table)
            {
                continue;
            }

            if (count == index)
            {
                return table.Table;
            }

            count++;
        }

        return null;
    }

    /// <summary>Composites a region onto the page, or keeps it when the segment is intermediate.</summary>
    /// <param name="segment">The segment.</param>
    /// <param name="info">The region information.</param>
    /// <param name="region">The region, which this method now owns.</param>
    /// <param name="intermediate">Whether the segment type is the intermediate one.</param>
    /// <param name="page">The page.</param>
    /// <returns><see cref="Jbig2Status.Success"/>, or <see cref="Jbig2Status.Failure"/> when the budget is spent.</returns>
    private Jbig2Status Finish(Jbig2Segment segment, in Jbig2RegionInfo info, Jbig2Bitmap region, bool intermediate, Jbig2Bitmap page)
    {
        if (intermediate)
        {
            segment.Bitmap = region;
            return Jbig2Status.Success;
        }

        var composed = Jbig2Composer.Compose(page, region.View, info.X, info.Y, info.Operator);
        region.Dispose();
        return _workspace.TryCharge(composed) ? Jbig2Status.Success : Jbig2Status.Failure;
    }
}
