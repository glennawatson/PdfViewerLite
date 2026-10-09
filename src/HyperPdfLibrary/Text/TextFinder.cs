// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>
/// Finds a query in page text, as PDFium's CPDF_TextPageFind does: the query is split into words at spaces and at
/// characters of scripts written without spaces, and words may be separated in the text by spaces and line breaks.
/// </summary>
internal ref struct TextFinder
{
    /// <summary>The no-break space.</summary>
    private const char NoBreakSpace = (char)0x00A0;

    /// <summary>The highest Latin-1 character, below which characters belong to space-separated scripts.</summary>
    private const char Latin1Limit = (char)0x00FF;

    /// <summary>The script-letter l, which PDFium treats as space-separated.</summary>
    private const char ScriptSmallL = (char)0x2113;

    /// <summary>The first Latin ligature, U+FB00.</summary>
    private const char FirstLigature = (char)0xFB00;

    /// <summary>The Latin ligature U+FB06.</summary>
    private const char LastLigature = (char)0xFB06;

    /// <summary>The stride of the pairs in <see cref="SpacedBlocks"/>.</summary>
    private const int PairStride = 2;

    /// <summary>The page text, lowered unless matching case.</summary>
    private readonly ReadOnlySpan<char> _text;

    /// <summary>The query, lowered unless matching case.</summary>
    private readonly ReadOnlySpan<char> _query;

    /// <summary>The query's words, as ranges of <see cref="_query"/>.</summary>
    private readonly ReadOnlySpan<TextSegment> _words;

    /// <summary>Whether only whole words match.</summary>
    private readonly bool _wholeWord;

    /// <summary>Whether the next search starts one character after the last match's start.</summary>
    private readonly bool _consecutive;

    /// <summary>Where the next search starts, or -1 when the text is empty.</summary>
    private int _next;

    /// <summary>Where the search for the current word starts.</summary>
    private int _start;

    /// <summary>Where the current word was found.</summary>
    private int _result;

    /// <summary>Whether the query starts with a space.</summary>
    private bool _spaceStart;

    /// <summary>Initializes a new instance of the <see cref="TextFinder"/> struct.</summary>
    /// <param name="text">The page text.</param>
    /// <param name="query">The query.</param>
    /// <param name="words">The query's words.</param>
    /// <param name="options">The search options.</param>
    internal TextFinder(ReadOnlySpan<char> text, ReadOnlySpan<char> query, ReadOnlySpan<TextSegment> words, PdfTextSearchOptions options)
    {
        _text = text;
        _query = query;
        _words = words;
        _wholeWord = (options & PdfTextSearchOptions.WholeWord) != 0;
        _consecutive = (options & PdfTextSearchOptions.Consecutive) != 0;
        _next = text.IsEmpty ? -1 : 0;
    }

    /// <summary>Gets the text index of the last match's first character.</summary>
    internal int MatchStart { get; private set; }

    /// <summary>Gets the text index of the last match's last character.</summary>
    internal int MatchEnd { get; private set; }

    /// <summary>Gets the first and last character of each block of space-separated scripts beyond Latin-1, as pairs.</summary>
    private static ReadOnlySpan<ushort> SpacedBlocks =>
    [
        0x0600, 0x06FF, 0xFE70, 0xFEFF, 0xFB50, 0xFDFF, 0x0400, 0x04FF, 0x0500, 0x052F, 0xA640, 0xA69F,
        0x2DE0, 0x2DFF, 0x2000, 0x206F,
    ];

    /// <summary>Splits a query into words, as PDFium's ExtractFindWhat does.</summary>
    /// <param name="query">The query.</param>
    /// <param name="words">Receives the words; at least two longer than the query.</param>
    /// <returns>The number of words.</returns>
    internal static int SplitWords(ReadOnlySpan<char> query, Span<TextSegment> words)
    {
        if (query.IndexOfAnyExcept(' ') < 0)
        {
            words[0] = new(0, query.Length, TextDirection.Neutral);
            return 1;
        }

        var count = 0;
        var position = 0;
        while (true)
        {
            var space = query[position..].IndexOf(' ');
            var end = space < 0 ? query.Length : position + space;
            count = AddWord(query, position, end - position, words, count);
            if (space < 0)
            {
                return count;
            }

            position = end + 1;
            while (position < query.Length && query[position] == ' ')
            {
                position++;
            }
        }
    }

    /// <summary>Determines whether a character is a space PDFium lets separate query words.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for spaces and line breaks.</returns>
    internal static bool IsGap(char value) => value is '\n' or ' ' or '\r' or NoBreakSpace;

    /// <summary>Determines whether a character belongs to a script written without spaces, so it is a word on its own.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for such characters.</returns>
    internal static bool IsUnspaced(char value)
    {
        if (value < Latin1Limit || value == ScriptSmallL)
        {
            return false;
        }

        var blocks = SpacedBlocks;
        for (var i = 0; i < blocks.Length; i += PairStride)
        {
            if (value >= blocks[i] && value <= blocks[i + 1])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Finds the next match, as PDFium's FindNext does.</summary>
    /// <returns><see langword="true"/> when a match was found.</returns>
    internal bool FindNext()
    {
        if (_next < 0 || _next >= _text.Length)
        {
            return false;
        }

        _start = _next;
        _result = 0;
        _spaceStart = false;
        var i = 0;
        while (i < _words.Length)
        {
            var step = _words[i].Count == 0 ? MatchEmptyWord(i) : MatchWord(i);
            if (step == WordStep.Fail)
            {
                return false;
            }

            i = step switch
            {
                WordStep.Restart => 0,
                WordStep.Done => _words.Length,
                _ => i + 1,
            };
        }

        MatchEnd = _result + _words[^1].Count - 1;
        _next = _consecutive ? MatchStart + 1 : MatchEnd + 1;
        return true;
    }

    /// <summary>Adds a word of the query, splitting out characters of scripts written without spaces.</summary>
    /// <param name="query">The query.</param>
    /// <param name="start">The word's start.</param>
    /// <param name="length">The word's length.</param>
    /// <param name="words">The words.</param>
    /// <param name="count">The words so far.</param>
    /// <returns>The words after adding.</returns>
    private static int AddWord(ReadOnlySpan<char> query, int start, int length, Span<TextSegment> words, int count)
    {
        if (length == 0)
        {
            // A leading or trailing space leaves an empty word, which matches a space in the text.
            words[count] = new(start, 0, TextDirection.Neutral);
            return count + 1;
        }

        var position = 0;
        while (position < length)
        {
            if (!IsUnspaced(query[start + position]))
            {
                position++;
                continue;
            }

            if (position > 0)
            {
                words[count] = new(start, position, TextDirection.Neutral);
                count++;
            }

            words[count] = new(start + position, 1, TextDirection.Neutral);
            count++;
            start += position + 1;
            length -= position + 1;
            position = 0;
        }

        if (length > 0)
        {
            words[count] = new(start, length, TextDirection.Neutral);
            count++;
        }

        return count;
    }

    /// <summary>Determines whether a match is a whole word, as PDFium's IsMatchWholeWord does.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The match's first character.</param>
    /// <param name="end">The match's last character.</param>
    /// <returns><see langword="true"/> when no letter or digit touches the match.</returns>
    private static bool IsWholeWord(ReadOnlySpan<char> text, int start, int end)
    {
        if (start > end)
        {
            return false;
        }

        var count = end - start + 1;
        if (count == 1 && text[start] > Latin1Limit)
        {
            return true;
        }

        var left = start >= 1 ? text[start - 1] : '\0';
        var right = start + count < text.Length ? text[start + count] : '\0';
        if (TouchesWord(left) || TouchesWord(right))
        {
            return false;
        }

        return !char.IsAsciiLetter(left) && !char.IsAsciiLetter(right);
    }

    /// <summary>Determines whether a neighbouring character continues a word, with PDFium's exclusive letter ranges.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool TouchesWord(char value) =>
        value is > 'A' and < 'a' or > 'a' and < 'z' or > FirstLigature and < LastLigature || char.IsAsciiDigit(value);

    /// <summary>Matches an empty word: a leading or trailing space in the query.</summary>
    /// <param name="index">The word index.</param>
    /// <returns>What to do next.</returns>
    private WordStep MatchEmptyWord(int index)
    {
        if (index != _words.Length - 1)
        {
            _spaceStart |= index == 0;
            return WordStep.Next;
        }

        if (_start >= _text.Length)
        {
            return WordStep.Fail;
        }

        if (!IsGap(_text[_start]))
        {
            return WordStep.Restart;
        }

        _result = _start + 1;
        return WordStep.Done;
    }

    /// <summary>Finds a word of the query and checks it continues the match.</summary>
    /// <param name="index">The word index.</param>
    /// <returns>What to do next.</returns>
    private WordStep MatchWord(int index)
    {
        var word = _words[index];
        var found = _text[_start..].IndexOf(_query.Slice(word.Start, word.Count));
        if (found < 0)
        {
            return WordStep.Fail;
        }

        _result = _start + found;
        var end = _result + word.Count - 1;
        if (index == 0)
        {
            MatchStart = _result;
        }

        if (IsWordMatch(index, end))
        {
            _start = end + 1;
            return WordStep.Next;
        }

        _start = MatchStart + _words[_spaceStart ? 1 : 0].Count;
        return WordStep.Restart;
    }

    /// <summary>Checks the gap before a word and the whole-word rule.</summary>
    /// <param name="index">The word index.</param>
    /// <param name="end">The word's last character.</param>
    /// <returns><see langword="true"/> when the word continues the match.</returns>
    private bool IsWordMatch(int index, int end)
    {
        var match = true;
        if (index != 0 && !_spaceStart)
        {
            match = IsJoinedToPrevious(index);
        }
        else if (_spaceStart && _result > 0)
        {
            match = IsGap(_text[_result - 1]);
            MatchStart = match ? _result - 1 : _result;
        }

        return match && (!_wholeWord || IsWholeWord(_text, _result, end));
    }

    /// <summary>Determines whether only spaces separate a word from the previous one.</summary>
    /// <param name="index">The word index.</param>
    /// <returns><see langword="true"/> when the word follows on.</returns>
    private readonly bool IsJoinedToPrevious(int index)
    {
        var current = _query[_words[index].Start];
        var previous = _words[index - 1];
        var last = previous.Count > 0 ? _query[previous.Start + previous.Count - 1] : '\0';
        if (_start == _result && !(IsUnspaced(last) || IsUnspaced(current)))
        {
            return false;
        }

        return _text[_start.._result].IndexOfAnyExcept(['\n', ' ', '\r', NoBreakSpace]) < 0;
    }
}
