// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Objects;

/// <content>Parsing indirect objects from the file.</content>
public sealed partial class PdfObjectStore
{
    /// <summary>The deepest nesting of object parses, which a stream whose /Length refers to itself would recurse into.</summary>
    private const int MaxParseDepth = 16;

    /// <summary>The first window an object is parsed from; most objects fit, and a larger one doubles it.</summary>
    private const int InitialWindow = 8 * 1024;

    /// <summary>The growth factor of an object's window.</summary>
    private const int WindowGrowth = 2;

    /// <summary>How many object parses the current thread is inside.</summary>
    [ThreadStatic]
    private static int _parseDepth;

    /// <summary>Parses the indirect object whose header is at an offset.</summary>
    /// <param name="offset">The offset of the object number.</param>
    /// <param name="id">The object id from the header.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the offset holds an object header.</returns>
    internal bool TryParseObjectAt(long offset, out PdfObjectId id, out PdfValue value)
    {
        id = default;
        value = default;
        if (offset < 0 || offset >= _source.Length)
        {
            return false;
        }

        if (_parseDepth >= MaxParseDepth)
        {
            PdfOpenContext.Report(Context, PdfDiagnosticCode.RecursionLimit, "Objects referred to each other too deeply while parsing.", 0, offset);
            return false;
        }

        _parseDepth++;
        try
        {
            return _array is not null ? TryParseInArray(_array, (int)offset, out id, out value) : TryParseInWindows(offset, out id, out value);
        }
        finally
        {
            _parseDepth--;
        }
    }

    /// <summary>Parses a value at an offset, such as a trailer dictionary, from a window that grows until the value fits.</summary>
    /// <param name="offset">The offset of the value.</param>
    /// <returns>The value; null when there is none.</returns>
    internal PdfValue ParseValueAt(long offset)
    {
        if (offset < 0 || offset >= _source.Length)
        {
            return default;
        }

        if (_array is not null)
        {
            var parser = new PdfParser(_array, (int)offset, this, Names);
            return parser.ParseValue();
        }

        var window = FirstWindow(offset);
        while (true)
        {
            PdfValue value;
            bool truncated;
            using (var lease = _source.Lease(offset, window))
            {
                var parser = PdfParser.ForWindow(lease.Span, 0, this, Names, null);
                value = parser.ParseValue();
                truncated = parser.ReachedEnd;
            }

            if (!truncated || !TryGrowWindow(offset, ref window))
            {
                return value;
            }
        }
    }

    /// <summary>Reads "number generation obj".</summary>
    /// <param name="parser">The parser, at the header.</param>
    /// <param name="id">The object id.</param>
    /// <returns><see langword="true"/> when a header was read.</returns>
    private static bool TryReadHeader(ref PdfParser parser, out PdfObjectId id)
    {
        id = default;
        if (parser.NextToken() != PdfTokenKind.Number || !PdfNumber.TryParse(parser.Lexeme, out var number) || number.Kind != PdfKind.Integer)
        {
            return false;
        }

        if (parser.NextToken() != PdfTokenKind.Number || !PdfNumber.TryParse(parser.Lexeme, out var generation))
        {
            return false;
        }

        // Some writers omit the space in "0obj"; the generation token then carries the keyword.
        if (!parser.Lexeme.EndsWith(PdfKeywords.Obj) && (parser.NextToken() != PdfTokenKind.Keyword || parser.Keyword != PdfKeyword.Obj))
        {
            return false;
        }

        id = new((int)Math.Clamp(number.AsInteger(), 0, PdfLimits.MaxObjectNumber), (int)Math.Clamp(generation.AsInteger(), 0, ushort.MaxValue));
        return true;
    }

    /// <summary>Parses an object's header and value, noting where a <c>stream</c> keyword after a dictionary ends.</summary>
    /// <param name="parser">The parser, at the header.</param>
    /// <param name="id">The object id.</param>
    /// <param name="value">The value.</param>
    /// <param name="streamKeywordEnd">The parser position after the <c>stream</c> keyword, or -1 when no stream follows.</param>
    /// <returns><see langword="true"/> when a header was read.</returns>
    private static bool TryParseObject(ref PdfParser parser, out PdfObjectId id, out PdfValue value, out int streamKeywordEnd)
    {
        value = default;
        streamKeywordEnd = -1;
        if (!TryReadHeader(ref parser, out id))
        {
            return false;
        }

        parser.EncryptionId = id;
        value = parser.ParseValue();
        if (value.Kind != PdfKind.Dictionary)
        {
            return true;
        }

        var mark = parser.Position;
        if (parser.NextToken() == PdfTokenKind.Keyword && parser.Keyword == PdfKeyword.Stream)
        {
            streamKeywordEnd = parser.Position;
        }
        else
        {
            parser.Position = mark;
        }

        return true;
    }

    /// <summary>Parses an object in place from a file held in one array.</summary>
    /// <param name="array">The file.</param>
    /// <param name="offset">The offset of the object number.</param>
    /// <param name="id">The object id.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the offset holds an object header.</returns>
    private bool TryParseInArray(byte[] array, int offset, out PdfObjectId id, out PdfValue value)
    {
        var parser = new PdfParser(array, offset, this, Names, Security);
        if (!TryParseObject(ref parser, out id, out value, out var keywordEnd))
        {
            return false;
        }

        if (keywordEnd >= 0)
        {
            value = ReadStream(value.AsDictionary()!, keywordEnd, id);
        }

        return true;
    }

    /// <summary>Parses an object from a window of the file, growing the window while the object runs past its end.</summary>
    /// <param name="offset">The offset of the object number.</param>
    /// <param name="id">The object id.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the offset holds an object header.</returns>
    private bool TryParseInWindows(long offset, out PdfObjectId id, out PdfValue value)
    {
        var window = FirstWindow(offset);
        while (true)
        {
            bool found;
            int keywordEnd;
            bool truncated;
            using (var lease = _source.Lease(offset, window))
            {
                var parser = PdfParser.ForWindow(lease.Span, 0, this, Names, Security);
                found = TryParseObject(ref parser, out id, out value, out keywordEnd);
                truncated = parser.ReachedEnd;
            }

            if (truncated && TryGrowWindow(offset, ref window))
            {
                continue;
            }

            if (found && keywordEnd >= 0)
            {
                value = ReadStream(value.AsDictionary()!, offset + keywordEnd, id);
            }

            return found;
        }
    }

    /// <summary>Gets the first window for a parse at an offset.</summary>
    /// <param name="offset">The offset.</param>
    /// <returns>The window length.</returns>
    private int FirstWindow(long offset) => (int)Math.Min(InitialWindow, _source.Length - offset);

    /// <summary>Doubles a window, up to the end of the file and the largest window allowed.</summary>
    /// <param name="offset">The window's offset.</param>
    /// <param name="window">The window length, grown.</param>
    /// <returns><see langword="false"/> when the window cannot grow.</returns>
    private bool TryGrowWindow(long offset, ref int window)
    {
        var limit = Math.Min(_source.Length - offset, PdfLimits.MaxObjectWindow);
        if (window >= limit)
        {
            if (limit == PdfLimits.MaxObjectWindow)
            {
                PdfOpenContext.Report(Context, PdfDiagnosticCode.RecursionLimit, "An object was larger than the largest window read and was cut short.", 0, offset);
            }

            return false;
        }

        window = (int)Math.Min((long)window * WindowGrowth, limit);
        return true;
    }

    /// <summary>Makes the stream whose <c>stream</c> keyword ends at an offset.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="keywordEnd">The offset after the keyword.</param>
    /// <param name="id">The object id.</param>
    /// <returns>The stream value.</returns>
    private PdfValue ReadStream(PdfDictionary dictionary, long keywordEnd, PdfObjectId id)
    {
        var start = SkipStreamLineEnd(keywordEnd);
        var length = StreamLength(dictionary, start, id);
        var encrypted = Security is not null && !dictionary.IsName(KnownName.Type, KnownName.XRef);
        var stream = _array is not null
            ? new PdfStream(dictionary, _array, (int)start, length, id, encrypted)
            : new PdfStream(dictionary, _source, start, length, id, encrypted);
        return PdfValue.FromStream(stream);
    }

    /// <summary>Skips the line end after the <c>stream</c> keyword, tolerating spaces before it.</summary>
    /// <param name="position">The offset after the keyword.</param>
    /// <returns>The offset of the data.</returns>
    private long SkipStreamLineEnd(long position)
    {
        position = PdfByteSearch.SkipAny(_source, position, PdfByteSearch.Space);
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

    /// <summary>Works out a stream's length from /Length, checking it against the <c>endstream</c> keyword.</summary>
    /// <param name="dictionary">The stream dictionary.</param>
    /// <param name="start">The offset of the data.</param>
    /// <param name="id">The stream's object id, for diagnostics.</param>
    /// <returns>The length of the data.</returns>
    private int StreamLength(PdfDictionary dictionary, long start, PdfObjectId id)
    {
        var declared = dictionary.Get(KnownName.Length).AsInteger(-1);
        if (declared is >= 0 and <= int.MaxValue && start + declared <= _source.Length && EndsAt(start + declared))
        {
            return (int)declared;
        }

        PdfOpenContext.Report(Context, PdfDiagnosticCode.BadStreamLength, "A stream's /Length was missing or wrong.", id.Number, start);
        return ScanStreamLength(start, id);
    }

    /// <summary>Finds a stream's length when /Length cannot be trusted: the data runs to "endstream", less the line end before it.</summary>
    /// <param name="start">The offset of the data.</param>
    /// <param name="id">The stream's object id, for diagnostics.</param>
    /// <returns>The length of the data.</returns>
    private int ScanStreamLength(long start, PdfObjectId id)
    {
        var found = PdfByteSearch.IndexOf(_source, PdfKeywords.EndStream, start);
        if (found < 0)
        {
            // Neither /Length nor endstream: the data runs to the object's endobj.
            PdfOpenContext.Report(Context, PdfDiagnosticCode.MissingEndStream, "A stream has no endstream keyword.", id.Number, start);
            found = PdfByteSearch.IndexOf(_source, "endobj"u8, start);
        }

        var end = found < 0 ? _source.Length : found;
        if (end > start && _source.ByteAt(end - 1) == '\n')
        {
            end--;
        }

        if (end > start && _source.ByteAt(end - 1) == '\r')
        {
            end--;
        }

        return (int)Math.Min(end - start, int.MaxValue);
    }

    /// <summary>Determines whether <c>endstream</c> follows an offset, after optional white space.</summary>
    /// <param name="offset">The offset.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool EndsAt(long offset) =>
        PdfByteSearch.StartsWith(_source, PdfByteSearch.SkipAny(_source, offset, PdfCharacters.Whitespace), PdfKeywords.EndStream);
}
