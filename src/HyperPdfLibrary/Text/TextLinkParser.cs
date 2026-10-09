// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Text;

/// <summary>Finds web and email addresses in page text, as PDFium's CPDF_LinkExtract does.</summary>
internal static class TextLinkParser
{
    /// <summary>The length a word must exceed to be checked.</summary>
    private const int MinLinkLength = 5;

    /// <summary>The length of <c>http</c>.</summary>
    private const int HttpLength = 4;

    /// <summary>The length of <c>www.</c>.</summary>
    private const int WwwLength = 4;

    /// <summary>The length of <c>://</c>.</summary>
    private const int SchemeSeparatorLength = 3;

    /// <summary>The fewest characters a domain name needs after the at sign.</summary>
    private const int MinDomainLength = 3;

    /// <summary>The copies of a word the scratch buffer holds: as written and in lower case.</summary>
    private const int ScratchCopies = 2;

    /// <summary>The stride of the pairs in <see cref="Brackets"/>.</summary>
    private const int PairStride = 2;

    /// <summary>The fewest characters a port takes after the closing bracket: the colon and one digit.</summary>
    private const int MinPortLength = 2;

    /// <summary>The step back from a doubled dot to the end of the host before it.</summary>
    private const int DoubledDotStep = 2;

    /// <summary>The character the page text holds for a joining hyphen.</summary>
    private const char JoiningHyphenText = (char)0xFFFE;

    /// <summary>The prefix added to bare <c>www.</c> hosts.</summary>
    private const string HttpPrefix = "http://";

    /// <summary>The prefix added to email addresses.</summary>
    private const string MailtoPrefix = "mailto:";

    /// <summary>Gets the opening brackets and quotes that cut a link at their partner, paired with that partner.</summary>
    private static ReadOnlySpan<char> Brackets => ['(', ')', '[', ']', '{', '}', '<', '>', '"', '"', '\'', '\''];

    /// <summary>Finds the links of a page.</summary>
    /// <param name="page">The text page.</param>
    /// <returns>The links.</returns>
    internal static PdfWebLink[] Extract(PdfTextPage page)
    {
        var text = page.Text;
        if (text.Length == 0)
        {
            return [];
        }

        var buffer = ArrayPool<char>.Shared.Rent(text.Length * ScratchCopies);
        try
        {
            return Scan(page, text, buffer);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    /// <summary>Walks the characters and checks each word.</summary>
    /// <param name="page">The text page.</param>
    /// <param name="text">The page text.</param>
    /// <param name="buffer">Scratch space for two copies of the text.</param>
    /// <returns>The links.</returns>
    private static PdfWebLink[] Scan(PdfTextPage page, string text, char[] buffer)
    {
        List<PdfWebLink>? links = null;
        var chars = page.Chars;
        var start = 0;
        var afterHyphen = false;
        var lineBreak = false;
        for (var position = 0; position < chars.Length; position++)
        {
            var info = chars[position];
            var last = position == chars.Length - 1;
            if (!last && IsWordChar(info))
            {
                afterHyphen = IsHyphenChar(info);
                continue;
            }

            if (!last && afterHyphen && IsLineBreak(info))
            {
                lineBreak = true;
                continue;
            }

            links = Add(links, CheckWord(text, start, position - start + (last ? 1 : 0), lineBreak, buffer));
            lineBreak = false;
            start = position + 1;
        }

        return links is null ? [] : [.. links];
    }

    /// <summary>Adds a link to the list when there is one, making the list on first use.</summary>
    /// <param name="links">The list, or <see langword="null"/> before the first link.</param>
    /// <param name="link">The link, or <see langword="null"/>.</param>
    /// <returns>The list.</returns>
    private static List<PdfWebLink>? Add(List<PdfWebLink>? links, PdfWebLink? link)
    {
        if (link is null)
        {
            return links;
        }

        links ??= [];
        links.Add(link);
        return links;
    }

    /// <summary>Determines whether a character is a hyphen, so a line break after it may continue a link.</summary>
    /// <param name="info">The character.</param>
    /// <returns><see langword="true"/> for joining hyphens and hyphen-minus glyphs.</returns>
    private static bool IsHyphenChar(in PdfTextChar info) =>
        info.Kind == PdfTextCharKind.Hyphen || (info.Kind == PdfTextCharKind.Normal && info.Unicode == '-');

    /// <summary>Determines whether a character is part of a line break.</summary>
    /// <param name="info">The character.</param>
    /// <returns><see langword="true"/> for CR and LF.</returns>
    private static bool IsLineBreak(in PdfTextChar info) => info.Unicode is '\n' or '\r';

    /// <summary>Determines whether a character continues a word.</summary>
    /// <param name="info">The character.</param>
    /// <returns><see langword="true"/> for shown characters other than spaces.</returns>
    private static bool IsWordChar(in PdfTextChar info) => info.Kind != PdfTextCharKind.Generated && info.Unicode != ' ';

    /// <summary>Checks one word for a link.</summary>
    /// <param name="text">The page text.</param>
    /// <param name="start">The word's first index, used as a text index as PDFium does.</param>
    /// <param name="count">The word's length.</param>
    /// <param name="lineBreak">Whether the word continues across a hyphenated line break.</param>
    /// <param name="buffer">Scratch space.</param>
    /// <returns>The link, or <see langword="null"/>.</returns>
    private static PdfWebLink? CheckWord(string text, int start, int count, bool lineBreak, char[] buffer)
    {
        var length = CopyWord(text, start, count, lineBreak, buffer);
        if (length <= MinLinkLength)
        {
            return null;
        }

        while (length > 0 && buffer[length - 1] is ')' or ',' or '>' or '.')
        {
            length--;
            count--;
        }

        if (count <= MinLinkLength)
        {
            return null;
        }

        var word = buffer.AsSpan(0, length);
        var lower = buffer.AsSpan(length, length);
        _ = word.ToLowerInvariant(lower);
        if (FindWebLink(word, lower) is { } web)
        {
            return web with { Start = web.Start + start };
        }

        return FindMailLink(word) is { } mail ? new(mail, start, count) : null;
    }

    /// <summary>Copies a word of the page text, dropping line breaks after a hyphen and turning joining hyphens back into hyphens.</summary>
    /// <param name="text">The page text.</param>
    /// <param name="start">The first index.</param>
    /// <param name="count">The length.</param>
    /// <param name="lineBreak">Whether to drop line breaks.</param>
    /// <param name="buffer">The destination.</param>
    /// <returns>The copied length.</returns>
    private static int CopyWord(string text, int start, int count, bool lineBreak, char[] buffer)
    {
        if (start >= text.Length || count <= 0)
        {
            return 0;
        }

        var length = 0;
        foreach (var value in text.AsSpan(start, Math.Min(count, text.Length - start)))
        {
            if (lineBreak && value is '\n' or '\r')
            {
                continue;
            }

            buffer[length] = value == JoiningHyphenText ? '-' : value;
            length++;
        }

        return length;
    }

    /// <summary>Finds an <c>http</c>, <c>https</c> or <c>www.</c> address in a word, as PDFium's CheckWebLink does.</summary>
    /// <param name="word">The word.</param>
    /// <param name="lower">The word in lower case.</param>
    /// <returns>The link with its start in the word, or <see langword="null"/>.</returns>
    private static PdfWebLink? FindWebLink(ReadOnlySpan<char> word, ReadOnlySpan<char> lower)
    {
        var start = lower.IndexOf("http".AsSpan());
        if (start >= 0 && FindSchemeEnd(lower, start) is var offset and > 0)
        {
            var end = FindLinkEnd(lower, offset, TrimBrackets(lower, start, lower.Length - 1));
            if (end > offset)
            {
                return new(word.Slice(start, end - start + 1).ToString(), start, end - start + 1);
            }
        }

        start = lower.IndexOf("www.".AsSpan());
        if (start < 0 || lower.Length <= start + WwwLength)
        {
            return null;
        }

        var hostEnd = FindLinkEnd(lower, start, TrimBrackets(lower, start, lower.Length - 1));
        if (hostEnd <= start + WwwLength)
        {
            return null;
        }

        var count = hostEnd - start + 1;
        return new(string.Concat(HttpPrefix, word.Slice(start, count)), start, count);
    }

    /// <summary>Finds the end of <c>http://</c> or <c>https://</c> at a position.</summary>
    /// <param name="lower">The word in lower case.</param>
    /// <param name="start">Where <c>http</c> starts.</param>
    /// <returns>The index after the scheme, or 0 when there is no full scheme.</returns>
    private static int FindSchemeEnd(ReadOnlySpan<char> lower, int start)
    {
        var offset = start + HttpLength;
        if (lower.Length <= offset + HttpLength)
        {
            return 0;
        }

        if (lower[offset] == 's')
        {
            offset++;
        }

        return lower[offset..].StartsWith("://".AsSpan()) ? offset + SchemeSeparatorLength : 0;
    }

    /// <summary>Cuts a link at the partner of each opening bracket or quote before it, as PDFium's TrimExternalBracketsFromWebLink does.</summary>
    /// <param name="lower">The word in lower case.</param>
    /// <param name="start">The link start.</param>
    /// <param name="end">The link end.</param>
    /// <returns>The new end.</returns>
    private static int TrimBrackets(ReadOnlySpan<char> lower, int start, int end)
    {
        var brackets = Brackets;
        for (var position = 0; position < start; position++)
        {
            var pair = brackets.IndexOf(lower[position]);
            if (pair >= 0 && pair % PairStride == 0)
            {
                end = TrimBackTo(lower, brackets[pair + 1], start, end);
            }
        }

        return end;
    }

    /// <summary>Moves a link end before the last occurrence of a character at or after the link start.</summary>
    /// <param name="lower">The word.</param>
    /// <param name="value">The character.</param>
    /// <param name="start">The link start.</param>
    /// <param name="end">The link end.</param>
    /// <returns>The new end.</returns>
    private static int TrimBackTo(ReadOnlySpan<char> lower, char value, int start, int end)
    {
        for (var position = end; position >= start; position--)
        {
            if (lower[position] == value)
            {
                return position - 1;
            }
        }

        return end;
    }

    /// <summary>Finds where a link's host ends, as PDFium's FindWebLinkEnding does.</summary>
    /// <param name="lower">The word in lower case.</param>
    /// <param name="start">The host start.</param>
    /// <param name="end">The last candidate index.</param>
    /// <returns>The last index of the link.</returns>
    private static int FindLinkEnd(ReadOnlySpan<char> lower, int start, int end)
    {
        if (lower[start..].Contains('/'))
        {
            return end;
        }

        if (lower[start] == '[')
        {
            return FindIpv6End(lower, start, end);
        }

        while (end > start && lower[end] < '\u0080')
        {
            if (char.IsAsciiDigit(lower[end]) || char.IsAsciiLetterLower(lower[end]) || lower[end] == '.')
            {
                break;
            }

            end--;
        }

        return end;
    }

    /// <summary>Finds the end of a bracketed IPv6 host and its port.</summary>
    /// <param name="lower">The word in lower case.</param>
    /// <param name="start">The opening bracket.</param>
    /// <param name="end">The last candidate index.</param>
    /// <returns>The last index of the link.</returns>
    private static int FindIpv6End(ReadOnlySpan<char> lower, int start, int end)
    {
        var close = lower[(start + 1)..].IndexOf(']');
        if (close < 0)
        {
            return end;
        }

        end = start + 1 + close;
        if (end <= start + 1)
        {
            return end;
        }

        var offset = end + 1;
        if (offset >= lower.Length || lower[offset] != ':')
        {
            return end;
        }

        offset++;
        while (offset < lower.Length && char.IsAsciiDigit(lower[offset]))
        {
            offset++;
        }

        return offset > end + MinPortLength ? offset - 1 : end;
    }

    /// <summary>Finds an email address in a word, as PDFium's CheckMailLink does.</summary>
    /// <param name="word">The word.</param>
    /// <returns>The <c>mailto:</c> address, or <see langword="null"/>.</returns>
    private static string? FindMailLink(ReadOnlySpan<char> word)
    {
        var at = word.IndexOf('@');
        if (at <= 0 || at == word.Length - 1 || !TrimLocalPart(ref word, at))
        {
            return null;
        }

        at = word.IndexOf('@');
        if (at <= 0)
        {
            return null;
        }

        word = word.TrimEnd('.');
        var dot = word[(at + 1)..].IndexOf('.');
        if (dot <= 0 || !TrimDomain(ref word, at))
        {
            return null;
        }

        return word.Contains(MailtoPrefix.AsSpan(), StringComparison.Ordinal) ? word.ToString() : string.Concat(MailtoPrefix, word);
    }

    /// <summary>Cuts the address start back to the first character that cannot be in the local part.</summary>
    /// <param name="word">The word; trimmed in place.</param>
    /// <param name="at">The at sign.</param>
    /// <returns><see langword="false"/> when the local part is invalid.</returns>
    private static bool TrimLocalPart(ref ReadOnlySpan<char> word, int at)
    {
        var dot = at;
        for (var i = at; i > 0; i--)
        {
            var value = word[i - 1];
            if (value is '_' or '-' || char.IsLetterOrDigit(value))
            {
                continue;
            }

            if (value != '.' || i == dot || i == 1)
            {
                if (i == at)
                {
                    return false;
                }

                word = word[(i == dot ? i + 1 : i)..];
                return true;
            }

            dot = i - 1;
        }

        return true;
    }

    /// <summary>Cuts the domain at the first character that cannot be in it.</summary>
    /// <param name="word">The word; trimmed in place.</param>
    /// <param name="at">The at sign.</param>
    /// <returns><see langword="false"/> when the domain is invalid.</returns>
    private static bool TrimDomain(ref ReadOnlySpan<char> word, int at)
    {
        var dot = 0;
        for (var i = at + 1; i < word.Length; i++)
        {
            var value = word[i];
            if (value == '-' || char.IsLetterOrDigit(value))
            {
                continue;
            }

            if (value != '.' || i == dot + 1)
            {
                var hostEnd = i == dot + 1 ? i - DoubledDotStep : i - 1;
                if (dot > 0 && hostEnd - at >= MinDomainLength)
                {
                    word = word[..(hostEnd + 1)];
                    return true;
                }

                return false;
            }

            dot = i;
        }

        return true;
    }
}
