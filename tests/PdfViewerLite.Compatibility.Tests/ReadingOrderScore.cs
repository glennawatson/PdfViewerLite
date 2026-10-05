// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace PdfViewerLite.Compatibility.Tests;

/// <summary>
/// Compares extracted reading text with a Markdown transcript in reading order: how many of the transcript's
/// paragraphs are found, and how many neighbouring paragraphs come out in the same order.
/// </summary>
/// <param name="Paragraphs">The transcript paragraphs long enough to compare.</param>
/// <param name="Found">How many of them were found in the extracted text.</param>
/// <param name="InOrder">How many neighbouring pairs of found paragraphs keep their order.</param>
[DebuggerDisplay("ReadingOrderScore: found {Found}/{Paragraphs}, in order {InOrder}/{Pairs}")]
internal readonly record struct ReadingOrderScore(int Paragraphs, int Found, int InOrder)
{
    /// <summary>The shortest paragraph, in letters and digits, worth comparing.</summary>
    private const int MinimumLength = 30;

    /// <summary>Halves a length.</summary>
    private const int Half = 2;

    /// <summary>The letters and digits used to find a paragraph.</summary>
    private const int KeyLength = 24;

    /// <summary>Gets the number of neighbouring pairs of found paragraphs.</summary>
    public int Pairs => Math.Max(0, Found - 1);

    /// <summary>Gets the share of paragraphs found.</summary>
    public double Coverage => Paragraphs == 0 ? 1 : (double)Found / Paragraphs;

    /// <summary>Gets the share of neighbouring pairs in order.</summary>
    public double Order => Pairs == 0 ? 1 : (double)InOrder / Pairs;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"found {Found}/{Paragraphs} ({Coverage:P0}), in order {InOrder}/{Pairs} ({Order:P0})");

    /// <summary>Scores extracted text against a transcript.</summary>
    /// <param name="markdown">The transcript.</param>
    /// <param name="extracted">The extracted reading text.</param>
    /// <returns>The score.</returns>
    internal static ReadingOrderScore Measure(string markdown, string extracted)
    {
        var text = Normalize(extracted);
        var keys = Keys(markdown);
        var positions = new List<int>();
        foreach (var key in keys)
        {
            // Repeated paragraphs (boilerplate, lorem ipsum) match their next copy after the previous paragraph first.
            var after = positions.Count == 0 ? 0 : positions[^1] + 1;
            var at = Find(text, key, after);
            if (at < 0)
            {
                at = Find(text, key, 0);
            }

            if (at >= 0)
            {
                positions.Add(at);
            }
        }

        var inOrder = 0;
        for (var i = 1; i < positions.Count; i++)
        {
            if (positions[i] > positions[i - 1])
            {
                inOrder++;
            }
        }

        return new(keys.Count, positions.Count, inOrder);
    }

    /// <summary>Gets the transcript's paragraphs long enough to compare, normalized, leaving out tables and comments.</summary>
    /// <param name="markdown">The transcript.</param>
    /// <returns>The normalized paragraphs.</returns>
    private static List<string> Keys(string markdown)
    {
        var keys = new List<string>();
        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '|' || line.StartsWith("<!--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = Normalize(line.TrimStart('#', '-', '*', ' '));
            if (key.Length >= MinimumLength)
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    /// <summary>Finds a paragraph by its start, or by its middle when the start is spelled differently.</summary>
    /// <param name="text">The normalized extracted text.</param>
    /// <param name="key">The normalized paragraph.</param>
    /// <param name="from">Where to start looking.</param>
    /// <returns>The position, or -1.</returns>
    private static int Find(string text, string key, int from)
    {
        var at = text.IndexOf(key[..KeyLength], from, StringComparison.Ordinal);
        return at >= 0 ? at : text.IndexOf(key.Substring((key.Length - KeyLength) / Half, KeyLength), from, StringComparison.Ordinal);
    }

    /// <summary>Keeps lower-case letters and digits, with ligatures and accents decomposed.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The normalized text.</returns>
    private static string Normalize(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (char.IsLetterOrDigit(c))
            {
                _ = builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }
}
