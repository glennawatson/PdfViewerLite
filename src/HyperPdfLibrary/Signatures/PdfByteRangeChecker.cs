// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Signatures;

/// <summary>
/// Checks a signature's /ByteRange: offset and length pairs in order, inside the file, not overlapping, with only the
/// /Contents hex string left out, and ending at a revision.
/// </summary>
internal static class PdfByteRangeChecker
{
    /// <summary>The largest /Contents accepted, in bytes: room for a CMS with embedded revocation data.</summary>
    internal const int MaxContentsLength = 1 << 24;

    /// <summary>The numbers per range: offset and length.</summary>
    private const int Pair = 2;

    /// <summary>The hex digits per byte.</summary>
    private const int DigitsPerByte = 2;

    /// <summary>The bytes read at a time when checking the tail of the file.</summary>
    private const int ScanWindow = 1 << 16;

    /// <summary>The bytes allowed between the angle brackets of a hex string.</summary>
    private static readonly SearchValues<byte> HexOrSpace = SearchValues.Create("0123456789abcdefABCDEF \t\r\n\f\0"u8);

    /// <summary>The PDF whitespace bytes.</summary>
    private static readonly SearchValues<byte> Whitespace = SearchValues.Create(" \t\r\n\f\0"u8);

    /// <summary>Checks a byte range against the file.</summary>
    /// <param name="range">The /ByteRange numbers.</param>
    /// <param name="contentsLength">The length of the decoded /Contents string.</param>
    /// <param name="file">The file.</param>
    /// <param name="revisions">The file's revisions.</param>
    /// <returns>The check.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfByteRangeCheck Check(long[] range, int contentsLength, byte[] file, PdfRevision[] revisions) =>
        Check(range, contentsLength, new MemoryPdfByteSource(file), revisions);

    /// <summary>Checks a byte range against the file, reading only the gaps and the tail.</summary>
    /// <param name="range">The /ByteRange numbers.</param>
    /// <param name="contentsLength">The length of the decoded /Contents string.</param>
    /// <param name="file">The file.</param>
    /// <param name="revisions">The file's revisions.</param>
    /// <returns>The check.</returns>
    internal static PdfByteRangeCheck Check(long[] range, int contentsLength, PdfByteSource file, PdfRevision[] revisions)
    {
        if (range.Length == 0)
        {
            return new(PdfByteRangeStatus.Missing, 0, 0, -1, false);
        }

        if (range.Length % Pair != 0)
        {
            return new(PdfByteRangeStatus.Malformed, 0, 0, -1, false);
        }

        long signedLength = 0;
        var status = contentsLength > MaxContentsLength ? PdfByteRangeStatus.TooLarge : CheckPairs(range, contentsLength, file, out signedLength);
        if (status != PdfByteRangeStatus.Valid)
        {
            return new(status, 0, 0, -1, false);
        }

        var end = range[^Pair] + range[^1];
        var covers = range[0] == 0 && IsBlank(file, end);
        return new(PdfByteRangeStatus.Valid, signedLength, end, FindRevision(revisions, end), covers);
    }

    /// <summary>Determines whether bytes are only whitespace.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns><see langword="true"/> when there is nothing but whitespace.</returns>
    internal static bool IsBlank(ReadOnlySpan<byte> bytes) => bytes.IndexOfAnyExcept(Whitespace) < 0;

    /// <summary>Determines whether a file holds only whitespace from an offset to its end, reading it a window at a time.</summary>
    /// <param name="file">The file.</param>
    /// <param name="from">The first offset checked.</param>
    /// <returns><see langword="true"/> when there is nothing but whitespace.</returns>
    internal static bool IsBlank(PdfByteSource file, long from)
    {
        for (var position = Math.Max(0, from); position < file.Length; position += ScanWindow)
        {
            using var window = file.Lease(position, (int)Math.Min(ScanWindow, file.Length - position));
            if (!IsBlank(window.Span))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Finds the revision that ends where the signed bytes end.</summary>
    /// <param name="revisions">The revisions.</param>
    /// <param name="end">The position after the last signed byte.</param>
    /// <returns>The revision's index, or -1.</returns>
    private static int FindRevision(PdfRevision[] revisions, long end)
    {
        foreach (var revision in revisions)
        {
            if (end >= revision.MarkerEnd && end <= revision.EndOffset)
            {
                return revision.Index;
            }
        }

        return -1;
    }

    /// <summary>Checks each pair and the gaps between them.</summary>
    /// <param name="range">The /ByteRange numbers.</param>
    /// <param name="contentsLength">The length of the decoded /Contents string.</param>
    /// <param name="file">The file.</param>
    /// <param name="signedLength">The total signed length.</param>
    /// <returns>The status.</returns>
    private static PdfByteRangeStatus CheckPairs(long[] range, int contentsLength, PdfByteSource file, out long signedLength)
    {
        signedLength = 0;
        long previousStart = 0;
        long previousEnd = 0;
        for (var i = 0; i < range.Length; i += Pair)
        {
            var start = range[i];
            var length = range[i + 1];
            if (start < 0 || length < 0)
            {
                return PdfByteRangeStatus.Malformed;
            }

            if (length > file.Length || start > file.Length - length)
            {
                return PdfByteRangeStatus.OutOfBounds;
            }

            var gap = i == 0 ? PdfByteRangeStatus.Valid : CheckGap(previousStart, previousEnd, start, contentsLength, file);
            if (gap != PdfByteRangeStatus.Valid)
            {
                return gap;
            }

            previousStart = start;
            previousEnd = start + length;
            signedLength += length;
        }

        return signedLength > Array.MaxLength ? PdfByteRangeStatus.TooLarge : PdfByteRangeStatus.Valid;
    }

    /// <summary>Checks that the gap before a range holds the /Contents hex string and nothing else.</summary>
    /// <param name="previousStart">The previous range's start.</param>
    /// <param name="previousEnd">The previous range's end.</param>
    /// <param name="start">This range's start.</param>
    /// <param name="contentsLength">The length of the decoded /Contents string.</param>
    /// <param name="file">The file.</param>
    /// <returns>The status.</returns>
    private static PdfByteRangeStatus CheckGap(long previousStart, long previousEnd, long start, int contentsLength, PdfByteSource file)
    {
        if (start < previousEnd)
        {
            return start < previousStart ? PdfByteRangeStatus.OutOfOrder : PdfByteRangeStatus.Overlapping;
        }

        // The gap holds at most the hex digits of the largest /Contents, with room for line breaks between them.
        if (start - previousEnd > (long)MaxContentsLength * DigitsPerByte * Pair)
        {
            return PdfByteRangeStatus.GapNotContents;
        }

        using var window = file.Lease(previousEnd, (int)(start - previousEnd));
        return CheckGapBytes(window.Span, contentsLength);
    }

    /// <summary>Checks that gap bytes are the /Contents hex string and nothing else.</summary>
    /// <param name="gap">The bytes between two ranges.</param>
    /// <param name="contentsLength">The length of the decoded /Contents string.</param>
    /// <returns>The status.</returns>
    private static PdfByteRangeStatus CheckGapBytes(ReadOnlySpan<byte> gap, int contentsLength)
    {
        if (gap.Length < Pair || gap[0] != (byte)'<' || gap[^1] != (byte)'>')
        {
            return PdfByteRangeStatus.GapNotContents;
        }

        var digits = gap[1..^1];
        if (digits.IndexOfAnyExcept(HexOrSpace) >= 0)
        {
            return PdfByteRangeStatus.GapNotContents;
        }

        var count = digits.Length - CountWhitespace(digits);
        return (count + 1) / DigitsPerByte == contentsLength ? PdfByteRangeStatus.Valid : PdfByteRangeStatus.GapNotContents;
    }

    /// <summary>Counts whitespace bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The count.</returns>
    private static int CountWhitespace(ReadOnlySpan<byte> bytes)
    {
        var count = 0;
        var at = bytes.IndexOfAny(Whitespace);
        while (at >= 0)
        {
            count++;
            bytes = bytes[(at + 1)..];
            at = bytes.IndexOfAny(Whitespace);
        }

        return count;
    }
}
