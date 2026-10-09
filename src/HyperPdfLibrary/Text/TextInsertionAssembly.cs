// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>Inserts spaces, line breaks and joining hyphens between runs.</summary>
internal static class TextInsertionAssembly
{
    /// <summary>How many thresholds above the previous baseline start a new line.</summary>
    internal const float LineAbove = 2;

    /// <summary>The divisor that turns the wider glyph width into the line threshold.</summary>
    internal const float LineThresholdDivisor = 4;

    /// <summary>The height a run must exceed before vertical overlap ends a horizontal line.</summary>
    private const float MinLineHeight = 4.5F;

    /// <summary>The share of the font size a run must be wide before it can end a vertical line.</summary>
    private const float MinColumnShare = 0.1F;

    /// <summary>The height an empty run must exceed to start a new line.</summary>
    private const float EmptyRunLineHeight = 5;

    /// <summary>How many thresholds below the previous baseline start a new line.</summary>
    private const float LineBelow = -3;

    /// <summary>The smallest display matrix x scale of an upright page.</summary>
    private const float UprightScale = 0.9F;

    /// <summary>The largest skew of an upright page or run.</summary>
    private const float UprightSkew = 0.1F;

    /// <summary>The width of the band PDFium checks for a run on the same line.</summary>
    private const float SameLineBand = 1000;

    /// <summary>The smallest normalised direction component that counts as a direction.</summary>
    private const float DirectionThreshold = 0.0872F;

    /// <summary>The distance below which two glyph origins are the same point.</summary>
    private const float SamePoint = 0.0001F;

    /// <summary>The first width step of the space threshold between runs.</summary>
    private const int SpaceStep1 = 400;

    /// <summary>The second width step of the space threshold between runs.</summary>
    private const int SpaceStep2 = 700;

    /// <summary>The third width step of the space threshold between runs.</summary>
    private const int SpaceStep3 = 800;

    /// <summary>The scale PDFium applies to two particular thresholds.</summary>
    private const float OddThresholdScale = 1.5F;

    /// <summary>The lower end of the first threshold PDFium scales.</summary>
    private const float OddThresholdLow1 = 1.4879F;

    /// <summary>The upper end of the first threshold PDFium scales.</summary>
    private const float OddThresholdHigh1 = 1.4881F;

    /// <summary>The lower end of the second threshold PDFium scales.</summary>
    private const float OddThresholdLow2 = 1.38999F;

    /// <summary>The upper end of the second threshold PDFium scales.</summary>
    private const float OddThresholdHigh2 = 1.39001F;

    /// <summary>The ASCII hyphen-minus.</summary>
    private const char HyphenMinus = '-';

    /// <summary>The soft hyphen.</summary>
    private const char SoftHyphen = (char)0x00AD;

    /// <summary>The character PDFium gives a joining hyphen.</summary>
    private const char JoiningHyphen = '\u0002';

    /// <summary>Scales a width threshold down in steps, as PDFium's NormalizeThreshold does.</summary>
    /// <param name="threshold">The threshold.</param>
    /// <param name="step1">The first step.</param>
    /// <param name="step2">The second step.</param>
    /// <param name="step3">The third step.</param>
    /// <returns>The scaled threshold.</returns>
    internal static float NormalizeThreshold(float threshold, int step1, int step2, int step3)
    {
        const float Below1 = 2;
        const float Below2 = 4;
        const float Below3 = 5;
        const float Above3 = 6;
        if (threshold < step1)
        {
            return threshold / Below1;
        }

        if (threshold < step2)
        {
            return threshold / Below2;
        }

        return threshold < step3 ? threshold / Below3 : threshold / Above3;
    }

    /// <summary>Processes the runs of a gathered line, as PDFium's ProcessTransformedTextObjects does.</summary>
    /// <param name="state">The reusable build state.</param>
    internal static void ProcessLine(TextPageBuildState state)
    {
        foreach (var index in state.Line)
        {
            ProcessLineRun(state, index);
        }
    }

    /// <summary>Determines whether a character is a hyphen.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for the hyphen-minus and the soft hyphen.</returns>
    private static bool IsHyphenCode(char value) => value is HyphenMinus or SoftHyphen;

    /// <summary>Determines whether two runs overlap vertically enough to be on different lines, as PDFium's EndHorizontalLine does.</summary>
    /// <param name="current">The current run's bounds.</param>
    /// <param name="previous">The previous run's bounds.</param>
    /// <returns><see langword="true"/> when the line ends.</returns>
    private static bool EndsHorizontalLine(in PdfRectangle current, in PdfRectangle previous)
    {
        if (current.Height <= MinLineHeight || previous.Height <= MinLineHeight)
        {
            return false;
        }

        return MathF.Max(current.Bottom, previous.Bottom) >= MathF.Min(current.Top, previous.Top);
    }

    /// <summary>Determines whether a run starts a new column, as PDFium's EndVerticalLine does.</summary>
    /// <param name="current">The current run's bounds.</param>
    /// <param name="previous">The previous run's bounds.</param>
    /// <param name="line">The current column's bounds.</param>
    /// <param name="currentSize">The current run's font size.</param>
    /// <param name="previousSize">The previous run's font size.</param>
    /// <returns><see langword="true"/> when the column ends.</returns>
    private static bool EndsVerticalLine(in PdfRectangle current, in PdfRectangle previous, in PdfRectangle line, float currentSize, float previousSize)
    {
        if (current.Width <= currentSize * MinColumnShare || previous.Width <= previousSize * MinColumnShare)
        {
            return false;
        }

        return MathF.Min(current.Right, line.Right) <= MathF.Max(current.Left, line.Left);
    }

    /// <summary>Decides whether a gap between runs is a space, as PDFium's GenerateSpace does.</summary>
    /// <param name="position">The current run's position in the previous run's space.</param>
    /// <param name="lastPosition">The previous glyph's position.</param>
    /// <param name="width">The current glyph's width.</param>
    /// <param name="lastWidth">The previous glyph's width.</param>
    /// <param name="threshold">The space threshold.</param>
    /// <returns><see langword="true"/> when a space goes between the runs.</returns>
    private static bool IsSpaceGap(Vector2 position, float lastPosition, float width, float lastWidth, float threshold)
    {
        if (MathF.Abs(lastPosition + lastWidth - position.X) <= threshold)
        {
            return false;
        }

        var thresholdPosition = threshold + lastWidth;
        var difference = position.X - lastPosition;
        if (MathF.Abs(difference) > thresholdPosition)
        {
            return true;
        }

        if (position.X < 0 && -thresholdPosition > difference)
        {
            return true;
        }

        return difference > width + lastWidth;
    }

    /// <summary>Gets the gap that makes a space between two runs.</summary>
    /// <param name="run">The run.</param>
    /// <param name="previous">The previous run.</param>
    /// <param name="item">The run's first glyph.</param>
    /// <param name="previousItem">The previous run's last glyph.</param>
    /// <param name="inverse">The inverse of the previous run's matrix.</param>
    /// <returns>The threshold in the previous run's space.</returns>
    private static float SpaceThresholdBetween(in TextRun run, in TextRun previous, in TextGlyph item, in TextGlyph previousItem, Matrix3x2 inverse)
    {
        var threshold = NormalizeThreshold(Math.Max(previousItem.WidthUnits, item.WidthUnits), SpaceStep1, SpaceStep2, SpaceStep3);
        if (previousItem.WidthUnits >= item.WidthUnits)
        {
            threshold *= MathF.Abs(previous.FontSize);
        }
        else
        {
            threshold *= MathF.Abs(run.FontSize);
            threshold = TextGeometry.TransformDistance(run.Matrix, threshold);
            threshold = TextGeometry.TransformDistance(inverse, threshold);
        }

        threshold /= TextGlyphAssembly.GlyphUnits;
        var odd = threshold is > OddThresholdLow1 and < OddThresholdHigh1 or > OddThresholdLow2 and < OddThresholdHigh2;
        return odd ? threshold * OddThresholdScale : threshold;
    }

    /// <summary>Adds one run of a line, with what goes before it.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="index">The run.</param>
    private static void ProcessLineRun(TextPageBuildState state, int index)
    {
        var run = state.Runs[index];
        if (MathF.Abs(run.Rect.Width) < TextRunCollection.SizeEpsilon)
        {
            return;
        }

        var actualText = TextLineAssembly.ActualTextStateOf(state, run);
        if (actualText == ActualTextState.Done)
        {
            state.Previous = index;
            return;
        }

        if (!InsertBefore(state, index, run))
        {
            return;
        }

        var charStart = state.Temp.Count;
        var textStart = state.TempText.Count;
        var reverse = actualText == ActualTextState.Replace ? TextLineAssembly.AddActualText(state, index) : TextGlyphAssembly.AddRunGlyphs(state, index);
        state.Previous = index;
        if (!reverse)
        {
            return;
        }

        state.Temp.Reverse(charStart, state.Temp.Count - charStart);
        state.TempText.Reverse(textStart, state.TempText.Count - textStart);
    }

    /// <summary>Adds what goes between the previous run and a run, and tracks the line's bounds.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="index">The run index.</param>
    /// <param name="run">The run.</param>
    /// <returns><see langword="false"/> when the run adds nothing.</returns>
    private static bool InsertBefore(TextPageBuildState state, int index, in TextRun run)
    {
        if (state.Previous < 0)
        {
            state.LineRect = run.Rect;
            return true;
        }

        var insertion = InsertionBefore(state, index);
        state.LineRect = insertion == TextInsertion.LineBreak ? run.Rect : TextGeometry.Union(state.LineRect, run.Rect);
        return Insert(state, insertion, index);
    }

    /// <summary>Gets the character added last, from the line being built or the page.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="previous">Receives the character.</param>
    /// <returns><see langword="true"/> when there is one.</returns>
    private static bool TryGetPreviousChar(TextPageBuildState state, out TextBuildChar previous)
    {
        if (state.Temp.Count > 0)
        {
            previous = state.Temp[^1];
            return true;
        }

        if (state.Chars.Count > 0)
        {
            previous = state.Chars[^1];
            return true;
        }

        previous = default;
        return false;
    }

    /// <summary>Works out a run's writing direction from its first and last glyphs, as PDFium's GetTextObjectWritingMode does.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="run">The run.</param>
    /// <returns>The direction.</returns>
    private static TextOrientation WritingModeOf(TextPageBuildState state, in TextRun run)
    {
        if (run.GlyphCount <= 1)
        {
            return state.LineDirection;
        }

        var glyphs = TextGlyphAssembly.GlyphsOf(state, run);
        var first = TextGeometry.Transform(run.Matrix, TextGlyphAssembly.ItemOrigin(run, glyphs[0]));
        var last = TextGeometry.Transform(run.Matrix, TextGlyphAssembly.ItemOrigin(run, glyphs[^1]));
        var delta = Vector2.Abs(last - first);
        if (delta.X <= SamePoint && delta.Y <= SamePoint)
        {
            return TextOrientation.Unknown;
        }

        var direction = Vector2.Normalize(delta);
        var underX = direction.X <= DirectionThreshold;
        if (direction.Y <= DirectionThreshold)
        {
            return underX ? state.LineDirection : TextOrientation.Horizontal;
        }

        return underX ? TextOrientation.Vertical : state.LineDirection;
    }

    /// <summary>Decides what goes between the previous run and a run, as PDFium's ProcessInsertObject does.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="index">The run.</param>
    /// <returns>The insertion.</returns>
    private static TextInsertion InsertionBefore(TextPageBuildState state, int index)
    {
        if (TryGetPreviousChar(state, out var last) && last.Run >= 0)
        {
            state.Previous = last.Run;
        }

        var run = state.Runs[index];
        var previous = state.Runs[state.Previous];
        var mode = WritingModeOf(state, run);
        if (mode == TextOrientation.Unknown)
        {
            mode = WritingModeOf(state, previous);
        }

        var item = TextGlyphAssembly.GlyphsOf(state, run)[0];
        var current = TextGlyphAssembly.FirstCharOf(state, item);
        if (EndsLine(state, mode, run, previous))
        {
            return IsHyphen(state, current) ? TextInsertion.Hyphen : TextInsertion.LineBreak;
        }

        return InsertionOnLine(state, mode, run, previous, item, current);
    }

    /// <summary>Determines whether the run bounds alone end the line.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="mode">The writing direction.</param>
    /// <param name="run">The run.</param>
    /// <param name="previous">The previous run.</param>
    /// <returns><see langword="true"/> when the line ends.</returns>
    private static bool EndsLine(TextPageBuildState state, TextOrientation mode, in TextRun run, in TextRun previous) => mode switch
    {
        TextOrientation.Horizontal => EndsHorizontalLine(run.Rect, previous.Rect),
        TextOrientation.Vertical => EndsVerticalLine(run.Rect, previous.Rect, state.LineRect, run.FontSize, previous.FontSize),
        _ => false,
    };

    /// <summary>Decides what goes between two runs that the bounds do not separate.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="mode">The writing direction.</param>
    /// <param name="run">The run.</param>
    /// <param name="previous">The previous run.</param>
    /// <param name="item">The run's first glyph.</param>
    /// <param name="current">The run's first character.</param>
    /// <returns>The insertion.</returns>
    private static TextInsertion InsertionOnLine(TextPageBuildState state, TextOrientation mode, in TextRun run, in TextRun previous, in TextGlyph item, char current)
    {
        var previousItem = TextGlyphAssembly.GlyphsOf(state, previous)[^1];
        var lastWidth = MathF.Abs(previousItem.WidthUnits * previous.FontSize / TextGlyphAssembly.GlyphUnits);
        var width = MathF.Abs(item.WidthUnits * run.FontSize / TextGlyphAssembly.GlyphUnits);
        var inverse = TextGeometry.Invert(previous.Matrix);
        var position = TextGeometry.Transform(inverse, run.Position);
        var threshold = MathF.Max(lastWidth, width) / LineThresholdDivisor;
        threshold = lastWidth < width ? TextGeometry.TransformDistance(inverse, threshold) : threshold;
        if (mode == TextOrientation.Horizontal && IsNewLine(state, run, previous, position, threshold))
        {
            return IsHyphen(state, current) ? TextInsertion.Hyphen : TextInsertion.LineBreak;
        }

        if (IsLoneHyphen(state, run, current))
        {
            return TextInsertion.Hyphen;
        }

        if (current == ' ' || EndsWithSpace(state, previousItem))
        {
            return TextInsertion.None;
        }

        var spaceThreshold = SpaceThresholdBetween(run, previous, item, previousItem, inverse);
        return IsSpaceGap(position, TextGlyphAssembly.ItemOrigin(previous, previousItem).X, width, lastWidth, spaceThreshold) ? TextInsertion.Space : TextInsertion.None;
    }

    /// <summary>Determines whether a run is a single hyphen that joins a word broken across lines.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="run">The run.</param>
    /// <param name="current">The run's character.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsLoneHyphen(TextPageBuildState state, in TextRun run, char current) => run.GlyphCount == 1 && IsHyphenCode(current) && IsHyphen(state, current);

    /// <summary>Determines whether a glyph's text ends with a space.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool EndsWithSpace(TextPageBuildState state, in TextGlyph glyph)
    {
        var text = TextGlyphAssembly.TextOf(state, glyph);
        return !text.IsEmpty && text[^1] == ' ';
    }

    /// <summary>Determines whether a run starts below or above the previous run's line, as PDFium decides for horizontal text.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="run">The run.</param>
    /// <param name="previous">The previous run.</param>
    /// <param name="position">The run's position in the previous run's space.</param>
    /// <param name="threshold">The line threshold.</param>
    /// <returns><see langword="true"/> when the run starts a new line.</returns>
    private static bool IsNewLine(TextPageBuildState state, in TextRun run, in TextRun previous, Vector2 position, float threshold)
    {
        var rect = previous.Rect;
        var offLine = (position.Y > threshold * LineAbove || position.Y < threshold * LineBelow) && (MathF.Abs(position.Y) >= 1 || MathF.Abs(position.Y) > MathF.Abs(position.X));
        if (!(TextGeometry.IsEmpty(rect) && rect.Height > EmptyRunLineHeight) && !offLine)
        {
            return false;
        }

        return previous.GlyphCount <= 1 || !SharesLineBand(state, run, previous);
    }

    /// <summary>Determines whether two upright runs share a band across the page, which keeps them on one line.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="run">The run.</param>
    /// <param name="previous">The previous run.</param>
    /// <returns><see langword="true"/> when they share a band.</returns>
    private static bool SharesLineBand(TextPageBuildState state, in TextRun run, in TextRun previous)
    {
        var glyphs = TextGlyphAssembly.GlyphsOf(state, previous);
        var matrix = previous.Matrix;
        var upright = state.Display.M11 > UprightScale && state.Display.M12 < UprightSkew && state.Display.M21 < UprightSkew && state.Display.M22 < -UprightScale
            && matrix.M12 < UprightSkew && matrix.M21 < UprightSkew;
        if (!upright || TextGlyphAssembly.ItemOrigin(previous, glyphs[^1]).X <= TextGlyphAssembly.ItemOrigin(previous, glyphs[0]).X)
        {
            return false;
        }

        return TextGeometry.Contains(new(0, previous.Rect.Bottom, SameLineBand, previous.Rect.Top), run.Position)
            || TextGeometry.Contains(new(0, run.Rect.Bottom, SameLineBand, run.Rect.Top), previous.Position);
    }

    /// <summary>Determines whether the text so far ends with a hyphen that joins a word to the next line, as PDFium's IsHyphen does.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="current">The next run's first character.</param>
    /// <returns><see langword="true"/> when the word continues.</returns>
    private static bool IsHyphen(TextPageBuildState state, char current)
    {
        var text = state.TempText.Count > 0 ? state.TempText : state.Text;
        if (text.Count == 0)
        {
            return false;
        }

        var span = CollectionsMarshal.AsSpan(text);
        var i = Math.Max(span.LastIndexOfAnyExcept(' '), 0);
        if (!IsHyphenCode(span[i]))
        {
            return false;
        }

        return (i > 0 && char.IsLetter(span[i - 1]) && char.IsLetterOrDigit(current)) || IsPreviousHyphenPiece(state);
    }

    /// <summary>Determines whether the last character is a hyphen from a decomposed glyph or /ActualText.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsPreviousHyphenPiece(TextPageBuildState state) =>
        TryGetPreviousChar(state, out var previous)
        && previous.Kind is PdfTextCharKind.Piece or PdfTextCharKind.ActualText
        && IsHyphenCode(previous.Unicode);

    /// <summary>Applies an insertion, as PDFium's ProcessGenerateCharacter does.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="insertion">The insertion.</param>
    /// <param name="index">The run that follows it.</param>
    /// <returns><see langword="false"/> when the run is a lone joining hyphen and adds nothing.</returns>
    private static bool Insert(TextPageBuildState state, TextInsertion insertion, int index)
    {
        switch (insertion)
        {
            case TextInsertion.Space:
                {
                    AppendGenerated(state, ' ', true);
                    return true;
                }

            case TextInsertion.LineBreak:
                {
                    TextLineAssembly.CloseTempLine(state);
                    if (state.Text.Count > 0)
                    {
                        AppendGenerated(state, '\r', false);
                        AppendGenerated(state, '\n', false);
                    }

                    return true;
                }

            case TextInsertion.Hyphen:
                {
                    return JoinHyphen(state, index);
                }

            default:
                {
                    return true;
                }
        }
    }

    /// <summary>Marks the hyphen ending the line being built as a joining hyphen.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="index">The run that follows it.</param>
    /// <returns><see langword="false"/> when the run is itself a lone hyphen.</returns>
    private static bool JoinHyphen(TextPageBuildState state, int index)
    {
        var run = state.Runs[index];
        if (run.GlyphCount == 1 && IsHyphenCode(TextGlyphAssembly.FirstCharOf(state, TextGlyphAssembly.GlyphsOf(state, run)[0])))
        {
            return false;
        }

        while (state.TempText.Count > 0 && state.TempText[^1] == ' ')
        {
            state.TempText.RemoveAt(state.TempText.Count - 1);
            state.Temp.RemoveAt(state.Temp.Count - 1);
        }

        if (state.Temp.Count == 0)
        {
            return true;
        }

        var last = state.Temp.Count - 1;
        state.Temp[last] = state.Temp[last] with
        {
            Kind = PdfTextCharKind.Hyphen,
            Unicode = JoiningHyphen
        };
        state.TempText[^1] = TextLineAssembly.NoText;
        return true;
    }

    /// <summary>Appends a generated space or line break after the last character, as PDFium's GenerateCharInfo places it.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="value">The character.</param>
    /// <param name="toLine">Whether it goes into the line being built rather than the page.</param>
    private static void AppendGenerated(TextPageBuildState state, char value, bool toLine)
    {
        if (!TryGetPreviousChar(state, out var previous))
        {
            return;
        }

        var fontSize = previous.Run >= 0 ? state.Runs[previous.Run].FontSize : previous.Box.Height;
        if (fontSize == 0)
        {
            fontSize = TextPageAssembly.DefaultFontSize;
        }

        var width = previous.Run >= 0 && previous.Code != -1 ? previous.WidthUnits : 0;
        var origin = new Vector2(previous.Origin.X + (width * fontSize / TextGlyphAssembly.GlyphUnits), previous.Origin.Y);
        var box = new PdfRectangle(origin.X, origin.Y, origin.X, origin.Y);
        var generated = new TextBuildChar(PdfTextCharKind.Generated, -1, value, origin, box, box, Matrix3x2.Identity, -1, 0);
        if (toLine)
        {
            state.TempText.Add(value);
            state.Temp.Add(generated);
        }
        else
        {
            state.Text.Add(value);
            state.Chars.Add(generated);
        }
    }
}
