// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>Assembles text lines, including ActualText and bidirectional order.</summary>
internal static class TextLineAssembly
{
    /// <summary>The character PDFium writes in the text for a glyph without Unicode, and for a joining hyphen.</summary>
    internal const char NoText = (char)0xFFFE;

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
    internal static bool IsNormal(in TextBuildChar info) => info.Unicode == '\0' ? info.Code != 0 : info.Kind == PdfTextCharKind.Hyphen || !ControlCharacters.Contains(info.Unicode);

    /// <summary>Works out how a run's /ActualText applies, as PDFium's PreMarkedContent does.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="run">The run.</param>
    /// <returns>The state.</returns>
    internal static ActualTextState ActualTextStateOf(TextPageBuildState state, in TextRun run)
    {
        var marks = run.Marks;
        if (marks.Count == 0 || !marks.HasActualText)
        {
            return ActualTextState.Pass;
        }

        if (IsSameSpanAsPrevious(state, marks))
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

    /// <summary>Adds a run's /ActualText in place of its glyphs, spreading the characters across the run's bounds.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="index">The run.</param>
    /// <returns><see langword="true"/> when the text is right to left and must be reversed back into logical order.</returns>
    internal static bool AddActualText(TextPageBuildState state, int index)
    {
        var run = state.Runs[index];
        var text = run.Marks.LastActualText;
        if (text.Length == 0)
        {
            return false;
        }

        var rightToLeft = TextGlyphAssembly.IsRightToLeft(state, run) || (state.Previous >= 0 && TextGlyphAssembly.IsRightToLeft(state, state.Runs[state.Previous]) && TextUnicode.IsRightToLeft(text));
        var rect = run.Rect;
        var share = rect.Width / text.Length;
        rect = rightToLeft ? rect with { Left = rect.Right - share } : rect with { Right = rect.Left + share };
        AddActualTextChars(state, index, text, rect, rightToLeft ? -rect.Width : rect.Width);
        return rightToLeft;
    }

    /// <summary>Moves the line being built to the page in bidi order, as PDFium's CloseTempLine does.</summary>
    /// <param name="state">The reusable build state.</param>
    internal static void CloseTempLine(TextPageBuildState state)
    {
        if (state.Temp.Count == 0)
        {
            return;
        }

        CollapseSpaces(state);
        var rightToLeft = FindSegments(state);
        var current = rightToLeft ? TextDirection.Right : TextDirection.Left;
        foreach (var segment in CollectionsMarshal.AsSpan(state.Segments))
        {
            current = AddSegment(state, segment, current);
        }

        state.Temp.Clear();
        state.TempText.Clear();
        state.Segments.Clear();
    }

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

    /// <summary>Determines whether the previous run was in the same /ActualText span, whose text it already added.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="marks">The run's marks.</param>
    /// <returns><see langword="true"/> when it was.</returns>
    private static bool IsSameSpanAsPrevious(TextPageBuildState state, TextMarks marks)
    {
        if (state.Previous < 0)
        {
            return false;
        }

        var previous = state.Runs[state.Previous].Marks;
        return previous.Count == marks.Count && ReferenceEquals(previous.Innermost, marks.Innermost);
    }

    /// <summary>Adds the characters of /ActualText, each with its share of the run's bounds.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="index">The run.</param>
    /// <param name="text">The text.</param>
    /// <param name="rect">The first character's box.</param>
    /// <param name="step">The distance between boxes.</param>
    private static void AddActualTextChars(TextPageBuildState state, int index, string text, PdfRectangle rect, float step)
    {
        var run = state.Runs[index];
        for (var k = 0; k < text.Length; k++)
        {
            var value = text[k] <= AsciiLimit && !IsPrintable(text[k]) ? ' ' : text[k];
            if (value >= ReplacementCharacter)
            {
                continue;
            }

            var offset = k * step;
            var box = new PdfRectangle(rect.Left + offset, rect.Bottom, rect.Right + offset, rect.Top);
            state.TempText.Add(value);
            state.Temp.Add(new(PdfTextCharKind.ActualText, -1, value, run.Position, box, box, run.Matrix, index, 0));
        }
    }

    /// <summary>Removes the second of each pair of adjacent spaces in the line being built.</summary>
    /// <param name="state">The reusable build state.</param>
    private static void CollapseSpaces(TextPageBuildState state)
    {
        var previousSpace = false;
        var write = 0;
        var count = state.TempText.Count;
        for (var read = 0; read < count; read++)
        {
            var value = state.TempText[read];
            var space = value == ' ';
            if (space && previousSpace)
            {
                continue;
            }

            if (write != read)
            {
                state.TempText[write] = value;
                state.Temp[write] = state.Temp[read];
            }

            write++;
            previousSpace = space;
        }

        if (write == count)
        {
            return;
        }

        state.TempText.RemoveRange(write, count - write);
        state.Temp.RemoveRange(write, count - write);
    }

    /// <summary>Splits the line being built into direction segments, in display order.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <returns><see langword="true"/> when the line reads right to left.</returns>
    private static bool FindSegments(TextPageBuildState state)
    {
        state.Segments.Clear();
        var text = CollectionsMarshal.AsSpan(state.TempText);
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

            state.Segments.Add(new(start, i - start, direction));
            right += direction == TextDirection.Right ? 1 : 0;
            left += direction == TextDirection.Left ? 1 : 0;
            start = i;
        }

        var rightToLeft = right > left || state.RightToLeft;
        if (rightToLeft)
        {
            state.Segments.Reverse();
        }

        return rightToLeft;
    }

    /// <summary>Adds one direction segment of the line being built to the page.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="current">The direction in force before it.</param>
    /// <returns>The direction in force after it.</returns>
    private static TextDirection AddSegment(TextPageBuildState state, in TextSegment segment, TextDirection current)
    {
        var end = segment.Start + segment.Count;
        if (segment.Direction == TextDirection.Right || (segment.Direction == TextDirection.Neutral && current == TextDirection.Right))
        {
            // /ActualText is already in logical order, so only glyph text is reversed.
            if (segment.Count > 0 && state.Temp[segment.Start].Kind == PdfTextCharKind.ActualText)
            {
                AddRange(state, segment.Start, end, true);
            }
            else
            {
                for (var i = end - 1; i >= segment.Start; i--)
                {
                    AddChar(state, state.TempText[i], state.Temp[i], true);
                }
            }

            return TextDirection.Right;
        }

        AddRange(state, segment.Start, end, false);
        return segment.Direction == TextDirection.LeftWeak ? current : TextDirection.Left;
    }

    /// <summary>Adds characters of the line being built to the page in order.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="start">The first character.</param>
    /// <param name="end">The index after the last.</param>
    /// <param name="rightToLeft">Whether they are right-to-left text.</param>
    private static void AddRange(TextPageBuildState state, int start, int end, bool rightToLeft)
    {
        for (var i = start; i < end; i++)
        {
            AddChar(state, state.TempText[i], state.Temp[i], rightToLeft);
        }
    }

    /// <summary>Adds a character to the page, mirroring and decomposing as PDFium's AddCharInfo does.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="value">The character's text.</param>
    /// <param name="info">The character.</param>
    /// <param name="rightToLeft">Whether it is right-to-left text.</param>
    private static void AddChar(TextPageBuildState state, char value, in TextBuildChar info, bool rightToLeft)
    {
        if (!IsNormal(info))
        {
            state.Chars.Add(info);
            return;
        }

        if (rightToLeft)
        {
            value = TextUnicode.GetMirror(value);
        }

        if (!rightToLeft && !TextUnicode.IsLigature(value))
        {
            state.Text.Add(value);
            state.Chars.Add(info);
            return;
        }

        Span<char> parts = stackalloc char[TextUnicode.MaxDecomposition];
        var count = TextUnicode.Decompose(value, parts);
        var piece = info with
        {
            Kind = PdfTextCharKind.Piece
        };
        foreach (var part in parts[..count])
        {
            state.Chars.Add(piece with { Unicode = part });
            state.Text.Add(part);
        }
    }
}
