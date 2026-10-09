// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Text;

/// <summary>Creates text page snapshots and clears reusable build state.</summary>
internal static class TextPageAssembly
{
    /// <summary>The font size used for characters without a run.</summary>
    internal const float DefaultFontSize = 1;

    /// <summary>Drops references to the device's lists and resets the line state.</summary>
    /// <param name="state">The reusable build state.</param>
    internal static void Clear(TextPageBuildState state)
    {
        state.Chars.Clear();
        state.Temp.Clear();
        state.Text.Clear();
        state.TempText.Clear();
        state.Line.Clear();
        state.Segments.Clear();
        state.SpaceCodes.Clear();
        state.Device.Clear();
        state.Previous = -1;
        state.LineDirection = TextOrientation.Unknown;
        state.LineRect = default;
    }

    /// <summary>Makes the finished page from the characters and text.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <param name="page">The page.</param>
    /// <returns>The text page.</returns>
    internal static PdfTextPage CreatePage(TextPageBuildState state, PdfPage page)
    {
        var chars = new PdfTextChar[state.Chars.Count];
        var runs = new int[chars.Length];
        var segments = TextIndexSegments(state);
        var source = CollectionsMarshal.AsSpan(state.Chars);
        for (var i = 0; i < chars.Length; i++)
        {
            var info = source[i];
            var hasRun = info.Run >= 0;
            var run = hasRun ? state.Runs[info.Run] : default;
            runs[i] = info.Run;
            chars[i] = new(info.Unicode, info.Kind, info.Code, info.Origin, info.Box, info.LooseBox, hasRun ? run.FontSize : DefaultFontSize, hasRun ? run.RenderMode : 0, hasRun && run.Font.IsBold);
        }

        return new(page, chars, runs, new string(CollectionsMarshal.AsSpan(state.Text)), segments);
    }

    /// <summary>Finds the runs of characters that have text, which map character indexes to text indexes, as PDFium's char_indices_ does.</summary>
    /// <param name="state">The reusable build state.</param>
    /// <returns>The runs.</returns>
    private static TextSegment[] TextIndexSegments(TextPageBuildState state)
    {
        state.Segments.Clear();
        if (state.Chars.Count > 0)
        {
            state.Segments.Add(new(0, 0, TextDirection.Neutral));
        }

        var skipped = false;
        var chars = CollectionsMarshal.AsSpan(state.Chars);
        for (var i = 0; i < chars.Length; i++)
        {
            var last = state.Segments.Count - 1;
            if (chars[i].Kind == PdfTextCharKind.Generated || TextLineAssembly.IsNormal(chars[i]))
            {
                state.Segments[last] = state.Segments[last] with
                {
                    Count = state.Segments[last].Count + 1
                };
                skipped = true;
            }
            else if (skipped)
            {
                state.Segments.Add(new(i + 1, 0, TextDirection.Neutral));
                skipped = false;
            }
            else
            {
                state.Segments[last] = state.Segments[last] with
                {
                    Start = i + 1
                };
            }
        }

        return [.. state.Segments];
    }
}
