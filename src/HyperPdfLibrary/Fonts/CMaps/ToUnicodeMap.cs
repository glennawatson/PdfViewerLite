// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace HyperPdfLibrary.Fonts.CMaps;

/// <summary>
/// A ToUnicode CMap: maps character codes to Unicode text, including ligatures and surrogate pairs. All texts live in
/// one character buffer and ranges are stored as sorted ranges, so a lookup does not allocate. Immutable and safe to
/// read from many threads.
/// </summary>
[DebuggerDisplay("ToUnicodeMap: {Count} ranges")]
public sealed class ToUnicodeMap
{
    /// <summary>The space a single-code-point lookup decodes into.</summary>
    private const int ScratchLength = 16;

    /// <summary>The UTF-16 units of a surrogate pair.</summary>
    private const int PairLength = 2;

    /// <summary>The Unicode ranges; values are where each text starts in <see cref="_text"/>.</summary>
    private readonly CodeRangeMap _ranges;

    /// <summary>The texts, each stored as its length followed by its UTF-16 units.</summary>
    private readonly char[] _text;

    /// <summary>Initializes a new instance of the <see cref="ToUnicodeMap"/> class.</summary>
    /// <param name="ranges">The ranges.</param>
    /// <param name="text">The texts.</param>
    private ToUnicodeMap(CodeRangeMap ranges, char[] text)
    {
        _ranges = ranges;
        _text = text;
        var longest = 0;
        for (var i = 0; i < text.Length; i += text[i] + 1)
        {
            longest = Math.Max(longest, text[i]);
        }

        // Incrementing the last unit of a range's text can turn one unit into a surrogate pair.
        MaxLength = longest + 1;
    }

    /// <summary>Gets the number of ranges.</summary>
    public int Count => _ranges.Count;

    /// <summary>Gets the most UTF-16 units one code can map to; a destination this long always fits.</summary>
    public int MaxLength { get; }

    /// <summary>Parses a ToUnicode stream.</summary>
    /// <param name="data">The decoded stream.</param>
    /// <returns>The map.</returns>
    public static ToUnicodeMap Parse(ReadOnlySpan<byte> data)
    {
        var content = CMapParser.Parse(data);
        return new(content.Unicode.Build(false), [.. content.Text]);
    }

    /// <summary>Maps a code to Unicode text.</summary>
    /// <param name="code">The character code.</param>
    /// <param name="destination">Receives the UTF-16 text.</param>
    /// <param name="charsWritten">The number of UTF-16 units written.</param>
    /// <returns><see langword="true"/> when the code is mapped and the text fit.</returns>
    public bool TryGetUnicode(int code, Span<char> destination, out int charsWritten)
    {
        charsWritten = 0;
        if (!_ranges.TryFind((uint)code, out var offset, out var start))
        {
            return false;
        }

        var text = _text.AsSpan(start + 1, _text[start]);
        if (offset == 0)
        {
            charsWritten = text.Length;
            return text.TryCopyTo(destination);
        }

        return TryWriteIncremented(text, (int)offset, destination, out charsWritten);
    }

    /// <summary>Maps a code to a single code point.</summary>
    /// <param name="code">The character code.</param>
    /// <param name="codePoint">The code point.</param>
    /// <returns><see langword="true"/> when the code maps to exactly one code point.</returns>
    public bool TryGetCodePoint(int code, out int codePoint)
    {
        Span<char> text = stackalloc char[ScratchLength];
        codePoint = 0;
        if (!TryGetUnicode(code, text, out var written))
        {
            return false;
        }

        var status = Rune.DecodeFromUtf16(text[..written], out var rune, out var consumed);
        codePoint = rune.Value;
        return status == System.Buffers.OperationStatus.Done && consumed == written;
    }

    /// <summary>Writes a range's text with its last code point moved on by the code's offset in the range.</summary>
    /// <param name="text">The range's text.</param>
    /// <param name="offset">The code's offset from the start of the range.</param>
    /// <param name="destination">The destination.</param>
    /// <param name="charsWritten">The number of UTF-16 units written.</param>
    /// <returns><see langword="true"/> when the text fit.</returns>
    private static bool TryWriteIncremented(ReadOnlySpan<char> text, int offset, Span<char> destination, out int charsWritten)
    {
        charsWritten = 0;
        var pair = text.Length >= PairLength && char.IsSurrogatePair(text[^PairLength], text[^1]);
        var prefix = text[..(text.Length - (pair ? PairLength : 1))];
        var last = pair ? char.ConvertToUtf32(text[^PairLength], text[^1]) : text[^1];
        if (!prefix.TryCopyTo(destination))
        {
            return false;
        }

        if (!Rune.TryCreate(last + offset, out var rune))
        {
            rune = Rune.ReplacementChar;
        }

        if (!rune.TryEncodeToUtf16(destination[prefix.Length..], out var count))
        {
            return false;
        }

        charsWritten = prefix.Length + count;
        return true;
    }
}
