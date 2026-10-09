// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <content>Closing lines in bidi order, /ActualText and the finished page.</content>
internal sealed partial class TextPageBuilder
{
    /// <summary>The last ASCII value PDFium checks with isprint.</summary>
    private const char AsciiLimit = '\u0080';

    /// <summary>The first printable ASCII character.</summary>
    private const char FirstPrintable = ' ';

    /// <summary>The last printable ASCII character.</summary>
    private const char LastPrintable = '~';

    /// <summary>The first character PDFium treats as a non-character in /ActualText.</summary>
    private const char ReplacementCharacter = (char)0xFFFD;

    /// <summary>Gets the control characters PDFium leaves out of the page text.</summary>
    private static ReadOnlySpan<char> ControlCharacters => ['\u0002', '\u0003', '\u0093', '\u0094', '\u0096', '\u0097', '\u0098', NoText];

    /// <summary>Determines whether a character goes into the page text, as PDFium's IsNormalCharacter decides.</summary>
    /// <param name="info">The character.</param>
    /// <returns><see langword="true"/> when it has text.</returns>
    private static bool IsNormal(in TextBuildChar info) => info.Unicode == '\0'
        ? info.Code != 0
        : info.Kind == PdfTextCharKind.Hyphen || !ControlCharacters.Contains(info.Unicode);

    /// <summary>Determines whether an ASCII character is printable, as C's isprint does.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> when it is printable.</returns>
    private static bool IsPrintable(char value) => value is >= FirstPrintable and <= LastPrintable;

    /// <summary>Determines whether /ActualText has a character PDFium counts as printable.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it has one.</returns>
    private static bool HasPrintable(string text)
    {
        foreach (var value in text)
        {
            if (value is > AsciiLimit and < ReplacementCharacter || (value <= AsciiLimit && IsPrintable(value)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Works out how a run's /ActualText applies, as PDFium's PreMarkedContent does.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The state.</returns>
    private ActualTextState ActualTextStateOf(in TextRun run)
    {
        var marks = run.Marks;
        if (marks.Count == 0 || !marks.HasActualText)
        {
            return ActualTextState.Pass;
        }

        if (IsSameSpanAsPrevious(marks))
        {
            return ActualTextState.Done;
        }

        var text = marks.ActualText;
        if (string.IsNullOrEmpty(text))
        {
            return ActualTextState.Pass;
        }

        return HasPrintable(text) ? ActualTextState.Replace : ActualTextState.Done;
    }

    /// <summary>Determines whether the previous run was in the same /ActualText span, whose text it already added.</summary>
    /// <param name="marks">The run's marks.</param>
    /// <returns><see langword="true"/> when it was.</returns>
    private bool IsSameSpanAsPrevious(TextMarks marks)
    {
        if (_previous < 0)
        {
            return false;
        }

        var previous = _runs[_previous].Marks;
        return previous.Count == marks.Count && ReferenceEquals(previous.Innermost, marks.Innermost);
    }

    /// <summary>Adds a run's /ActualText in place of its glyphs, spreading the characters across the run's bounds.</summary>
    /// <param name="index">The run.</param>
    /// <returns><see langword="true"/> when the text is right to left and must be reversed back into logical order.</returns>
    private bool AddActualText(int index)
    {
        var run = _runs[index];
        var text = run.Marks.LastActualText;
        if (text.Length == 0)
        {
            return false;
        }

        var rightToLeft = IsRightToLeft(run)
            || (_previous >= 0 && IsRightToLeft(_runs[_previous]) && TextUnicode.IsRightToLeft(text));
        var rect = run.Rect;
        var share = rect.Width / text.Length;
        rect = rightToLeft ? rect with { Left = rect.Right - share } : rect with { Right = rect.Left + share };
        AddActualTextChars(index, text, rect, rightToLeft ? -rect.Width : rect.Width);
        return rightToLeft;
    }

    /// <summary>Adds the characters of /ActualText, each with its share of the run's bounds.</summary>
    /// <param name="index">The run.</param>
    /// <param name="text">The text.</param>
    /// <param name="rect">The first character's box.</param>
    /// <param name="step">The distance between boxes.</param>
    private void AddActualTextChars(int index, string text, PdfRectangle rect, float step)
    {
        var run = _runs[index];
        for (var k = 0; k < text.Length; k++)
        {
            var value = text[k] <= AsciiLimit && !IsPrintable(text[k]) ? ' ' : text[k];
            if (value >= ReplacementCharacter)
            {
                continue;
            }

            var offset = k * step;
            var box = new PdfRectangle(rect.Left + offset, rect.Bottom, rect.Right + offset, rect.Top);
            _tempText.Add(value);
            _temp.Add(new(PdfTextCharKind.ActualText, -1, value, run.Position, box, box, run.Matrix, index, 0));
        }
    }

    /// <summary>Moves the line being built to the page in bidi order, as PDFium's CloseTempLine does.</summary>
    private void CloseTempLine()
    {
        if (_temp.Count == 0)
        {
            return;
        }

        CollapseSpaces();
        var rightToLeft = FindSegments();
        var current = rightToLeft ? TextDirection.Right : TextDirection.Left;
        foreach (var segment in CollectionsMarshal.AsSpan(_segments))
        {
            current = AddSegment(segment, current);
        }

        _temp.Clear();
        _tempText.Clear();
        _segments.Clear();
    }

    /// <summary>Removes the second of each pair of adjacent spaces in the line being built.</summary>
    private void CollapseSpaces()
    {
        var previousSpace = false;
        for (var i = 0; i < _tempText.Count; i++)
        {
            if (_tempText[i] != ' ')
            {
                previousSpace = false;
                continue;
            }

            if (previousSpace)
            {
                _tempText.RemoveAt(i);
                _temp.RemoveAt(i);
                i--;
            }

            previousSpace = true;
        }
    }

    /// <summary>Splits the line being built into direction segments, in display order.</summary>
    /// <returns><see langword="true"/> when the line reads right to left.</returns>
    private bool FindSegments()
    {
        _segments.Clear();
        var text = CollectionsMarshal.AsSpan(_tempText);
        var right = 0;
        var left = 0;
        var start = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            var direction = TextUnicode.GetDirection(text[start]);
            if (i < text.Length && TextUnicode.GetDirection(text[i]) == direction)
            {
                continue;
            }

            _segments.Add(new(start, i - start, direction));
            right += direction == TextDirection.Right ? 1 : 0;
            left += direction == TextDirection.Left ? 1 : 0;
            start = i;
        }

        var rightToLeft = right > left || _rightToLeft;
        if (rightToLeft)
        {
            _segments.Reverse();
        }

        return rightToLeft;
    }

    /// <summary>Adds one direction segment of the line being built to the page.</summary>
    /// <param name="segment">The segment.</param>
    /// <param name="current">The direction in force before it.</param>
    /// <returns>The direction in force after it.</returns>
    private TextDirection AddSegment(in TextSegment segment, TextDirection current)
    {
        var end = segment.Start + segment.Count;
        if (segment.Direction == TextDirection.Right || (segment.Direction == TextDirection.Neutral && current == TextDirection.Right))
        {
            // /ActualText is already in logical order, so only glyph text is reversed.
            if (segment.Count > 0 && _temp[segment.Start].Kind == PdfTextCharKind.ActualText)
            {
                AddRange(segment.Start, end, true);
            }
            else
            {
                for (var i = end - 1; i >= segment.Start; i--)
                {
                    AddChar(_tempText[i], _temp[i], true);
                }
            }

            return TextDirection.Right;
        }

        AddRange(segment.Start, end, false);
        return segment.Direction == TextDirection.LeftWeak ? current : TextDirection.Left;
    }

    /// <summary>Adds characters of the line being built to the page in order.</summary>
    /// <param name="start">The first character.</param>
    /// <param name="end">The index after the last.</param>
    /// <param name="rightToLeft">Whether they are right-to-left text.</param>
    private void AddRange(int start, int end, bool rightToLeft)
    {
        for (var i = start; i < end; i++)
        {
            AddChar(_tempText[i], _temp[i], rightToLeft);
        }
    }

    /// <summary>Adds a character to the page, mirroring and decomposing as PDFium's AddCharInfo does.</summary>
    /// <param name="value">The character's text.</param>
    /// <param name="info">The character.</param>
    /// <param name="rightToLeft">Whether it is right-to-left text.</param>
    private void AddChar(char value, in TextBuildChar info, bool rightToLeft)
    {
        if (!IsNormal(info))
        {
            _chars.Add(info);
            return;
        }

        if (rightToLeft)
        {
            value = TextUnicode.GetMirror(value);
        }

        if (!rightToLeft && !TextUnicode.IsLigature(value))
        {
            _text.Add(value);
            _chars.Add(info);
            return;
        }

        Span<char> parts = stackalloc char[TextUnicode.MaxDecomposition];
        var count = TextUnicode.Decompose(value, parts);
        var piece = info with { Kind = PdfTextCharKind.Piece };
        foreach (var part in parts[..count])
        {
            _chars.Add(piece with { Unicode = part });
            _text.Add(part);
        }
    }

    /// <summary>Makes the finished page from the characters and text.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The text page.</returns>
    private PdfTextPage CreatePage(PdfPage page)
    {
        var chars = new PdfTextChar[_chars.Count];
        var runs = new int[chars.Length];
        var segments = TextIndexSegments();
        var source = CollectionsMarshal.AsSpan(_chars);
        for (var i = 0; i < chars.Length; i++)
        {
            var info = source[i];
            var hasRun = info.Run >= 0;
            var run = hasRun ? _runs[info.Run] : default;
            runs[i] = info.Run;
            chars[i] = new(
                info.Unicode,
                info.Kind,
                info.Code,
                info.Origin,
                info.Box,
                info.LooseBox,
                hasRun ? run.FontSize : DefaultFontSize,
                hasRun ? run.RenderMode : 0,
                hasRun && run.Font.IsBold);
        }

        return new(page, chars, runs, new string(CollectionsMarshal.AsSpan(_text)), segments);
    }

    /// <summary>Finds the runs of characters that have text, which map character indexes to text indexes, as PDFium's char_indices_ does.</summary>
    /// <returns>The runs.</returns>
    private TextSegment[] TextIndexSegments()
    {
        _segments.Clear();
        if (_chars.Count > 0)
        {
            _segments.Add(new(0, 0, TextDirection.Neutral));
        }

        var skipped = false;
        var chars = CollectionsMarshal.AsSpan(_chars);
        for (var i = 0; i < chars.Length; i++)
        {
            var last = _segments.Count - 1;
            if (chars[i].Kind == PdfTextCharKind.Generated || IsNormal(chars[i]))
            {
                _segments[last] = _segments[last] with { Count = _segments[last].Count + 1 };
                skipped = true;
            }
            else if (skipped)
            {
                _segments.Add(new(i + 1, 0, TextDirection.Neutral));
                skipped = false;
            }
            else
            {
                _segments[last] = _segments[last] with { Start = i + 1 };
            }
        }

        return [.. _segments];
    }
}
