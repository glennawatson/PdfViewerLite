// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace PdfViewerLite.Core.Speech;

/// <summary>
/// Splits a page's text into sentences for reading aloud, keeping their positions for the follow-along highlight. A
/// sentence ends at <c>.</c>, <c>!</c> or <c>?</c> before white space (not after a common abbreviation), or at a blank
/// line; very long runs are split at a comma or space so the voice never waits long. Allocates nothing beyond the list.
/// </summary>
public static class SentenceSplitter
{
    /// <summary>The longest sentence spoken in one piece, in characters.</summary>
    private const int MaxLength = 300;

    /// <summary>Characters that end a sentence.</summary>
    private static readonly SearchValues<char> Terminators = SearchValues.Create(".!?");

    /// <summary>Words that end with a full stop without ending the sentence.</summary>
    private static readonly string[] Abbreviations =
    [
        "mr", "mrs", "ms", "dr", "prof", "st", "vs", "etc", "e.g", "i.e", "fig", "no", "vol", "p", "pp",
        "inc", "ltd", "jr", "sr", "a.m", "p.m", "approx", "dept", "est",
    ];

    /// <summary>Splits text into sentences.</summary>
    /// <param name="text">The text.</param>
    /// <param name="sentences">The list receiving the sentences; cleared first.</param>
    public static void Split(ReadOnlySpan<char> text, List<SpeechSentence> sentences)
    {
        ArgumentNullException.ThrowIfNull(sentences);
        sentences.Clear();
        var start = SkipSpace(text, 0);
        for (var i = start; i < text.Length; i++)
        {
            if (!EndsSentence(text, i, start))
            {
                continue;
            }

            Add(text, start, i + 1, sentences);
            start = SkipSpace(text, i + 1);
            i = start - 1;
        }

        Add(text, start, text.Length, sentences);
    }

    /// <summary>Prepares a sentence for the voice: joins words broken over lines and makes line breaks spaces.</summary>
    /// <param name="sentence">The sentence as it appears on the page.</param>
    /// <returns>The text to speak.</returns>
    public static string ToSpeech(ReadOnlySpan<char> sentence)
    {
        var builder = new StringBuilder(sentence.Length);
        for (var i = 0; i < sentence.Length; i++)
        {
            var c = sentence[i];
            if (c == '-' && IsLineBreak(sentence, i + 1))
            {
                // A word hyphenated at the end of a line: drop the hyphen and the break.
                i = SkipLineBreaks(sentence, i + 1) - 1;
                continue;
            }

            if (!char.IsWhiteSpace(c) && !char.IsControl(c))
            {
                _ = builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != ' ')
            {
                _ = builder.Append(' ');
            }
        }

        return builder.ToString().Trim();
    }

    /// <summary>Determines whether a line break is at an index.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index.</param>
    /// <returns><see langword="true"/> for a carriage return or line feed.</returns>
    private static bool IsLineBreak(ReadOnlySpan<char> text, int index) => index < text.Length && text[index] is '\r' or '\n';

    /// <summary>Skips line breaks.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">Where to start.</param>
    /// <returns>The first character after them.</returns>
    private static int SkipLineBreaks(ReadOnlySpan<char> text, int index)
    {
        while (IsLineBreak(text, index))
        {
            index++;
        }

        return index;
    }

    /// <summary>Determines whether a sentence ends at a character.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The character.</param>
    /// <param name="start">Where the sentence started.</param>
    /// <returns><see langword="true"/> when it ends there.</returns>
    private static bool EndsSentence(ReadOnlySpan<char> text, int index, int start)
    {
        var next = index + 1 < text.Length ? text[index + 1] : ' ';
        var terminated = Terminators.Contains(text[index]) && char.IsWhiteSpace(next) && !IsAbbreviation(text, index);

        // A blank line ends a paragraph, and so a sentence; a very long run is split where it can be.
        var paragraph = text[index] == '\n' && next is '\n' or '\r' && index > start;
        var tooLong = index - start >= MaxLength - 1 && IsBreakable(text[index]);
        return terminated || paragraph || tooLong;
    }

    /// <summary>Determines whether a long run can be split after a character.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for commas, semicolons and spaces.</returns>
    private static bool IsBreakable(char value) => value is ',' or ';' or ' ';

    /// <summary>Determines whether a full stop ends an abbreviation such as "Dr.".</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The full stop.</param>
    /// <returns><see langword="true"/> for an abbreviation.</returns>
    private static bool IsAbbreviation(ReadOnlySpan<char> text, int index)
    {
        if (text[index] != '.')
        {
            return false;
        }

        var wordStart = index;
        while (wordStart > 0 && !char.IsWhiteSpace(text[wordStart - 1]))
        {
            wordStart--;
        }

        var word = text[wordStart..index].TrimStart("([\"'“");
        foreach (var abbreviation in Abbreviations)
        {
            if (word.Equals(abbreviation, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // A single capital letter, as in an initial: "J. Smith".
        return word.Length == 1 && char.IsUpper(word[0]);
    }

    /// <summary>Skips white space.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">Where to start.</param>
    /// <returns>The next non-space character.</returns>
    private static int SkipSpace(ReadOnlySpan<char> text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>Adds a sentence when it has something to say.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The first character.</param>
    /// <param name="end">The character after the last.</param>
    /// <param name="sentences">The list.</param>
    private static void Add(ReadOnlySpan<char> text, int start, int end, List<SpeechSentence> sentences)
    {
        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        if (end > start && text[start..end].ContainsAnyExcept(" \r\n\t.,;:!?-—…\"'()[]"))
        {
            sentences.Add(new(start, end - start));
        }
    }
}
