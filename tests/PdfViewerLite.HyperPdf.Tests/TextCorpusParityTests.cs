// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using PdfViewerLite.Core.Reading;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// Compares the managed text of the first pages of each cached corpus document with PDFium's: character counts, text
/// and the offset between the engines' character boxes. The numbers are written to the test output; the test is
/// skipped when the corpus is absent.
/// </summary>
[NotInParallel(nameof(TextCorpusParityTests))]
public sealed class TextCorpusParityTests
{
    /// <summary>The pages of each corpus file compared.</summary>
    private const int CorpusPages = 2;

    /// <summary>The share of pages whose text must match PDFium's exactly.</summary>
    private const double MinimumTextMatchRate = 0.9;

    /// <summary>The largest median box offset accepted on a page, in points.</summary>
    private const float MaximumMedianOffset = 1;

    /// <summary>Half, for the centre of a rectangle and the middle of a sorted list.</summary>
    private const float Half = 0.5F;

    /// <summary>The variable naming another corpus folder.</summary>
    private const string CorpusVariable = "PDFVIEWERLITE_CORPUS_DIR";

    /// <summary>The first pages of each corpus document give PDFium's text and character boxes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CorpusTextMatchesPdfium()
    {
        var folder = Environment.GetEnvironmentVariable(CorpusVariable) is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "corpus");
        var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.pdf") : [];
        if (files.Length == 0)
        {
            Skip.Test($"No corpus PDFs in {folder}.");
        }

        Array.Sort(files, StringComparer.Ordinal);
        var pages = new List<PageComparison>();
        foreach (var file in files)
        {
            using var pair = new EnginePair(await File.ReadAllBytesAsync(file));
            var count = Math.Min(CorpusPages, pair.Pdfium.PageCount);
            for (var page = 0; page < count; page++)
            {
                var comparison = Compare(pair, page);
                pages.Add(comparison);
                TestContext.Current?.Output.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Path.GetFileName(file)} page {page + 1}: chars {comparison.ExpectedCount}/{comparison.ActualCount}, "
                    + $"text {(comparison.TextMatches ? "equal" : "differs")}, median offset {comparison.MedianOffset:F3}"));
            }
        }

        var countMatches = pages.Count(static page => page.ExpectedCount == page.ActualCount);
        var textMatches = pages.Count(static page => page.TextMatches);
        var offsets = pages.Where(static page => page.ExpectedCount > 0 && page.ExpectedCount == page.ActualCount).Select(static page => page.MedianOffset).ToList();
        TestContext.Current?.Output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{pages.Count} pages: counts equal {countMatches}, text equal {textMatches} ({(double)textMatches / pages.Count:P1}), median of page median offsets {Median(offsets):F3}"));

        await Assert.That((double)textMatches / pages.Count).IsGreaterThanOrEqualTo(MinimumTextMatchRate);
        foreach (var offset in offsets)
        {
            await Assert.That(offset).IsLessThanOrEqualTo(MaximumMedianOffset);
        }
    }

    /// <summary>Compares one page's text.</summary>
    /// <param name="pair">The documents.</param>
    /// <param name="page">The page index.</param>
    /// <returns>The comparison.</returns>
    private static PageComparison Compare(EnginePair pair, int page)
    {
        var document = (HyperPdfDocument)pair.HyperPdf;
        var expectedCount = pair.Pdfium.GetCharacterCount(page);
        var actualCount = PdfViewerLite.HyperPdf.HyperPdfText.GetCharacterCountNative(document, page);
        var textMatches = expectedCount == actualCount
            && string.Equals(pair.Pdfium.GetText(page, 0, expectedCount), PdfViewerLite.HyperPdf.HyperPdfText.GetTextNative(document, page, 0, actualCount), StringComparison.Ordinal);
        var expected = new List<PageCharacter>();
        ((ITextLayoutSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(pair.Pdfium, typeof(ITextLayoutSource))!).GetCharacters(page, expected);
        var actual = new List<PageCharacter>();
        PdfViewerLite.HyperPdf.HyperPdfText.GetCharactersNative(document, page, actual);
        var offsets = new List<float>();
        for (var i = 0; i < Math.Min(expected.Count, actual.Count); i++)
        {
            if (expected[i].Generated || actual[i].Generated)
            {
                continue;
            }

            var e = expected[i].Bounds;
            var a = actual[i].Bounds;
            var dx = (a.Left + (a.Width * Half)) - (e.Left + (e.Width * Half));
            var dy = (a.Top + (a.Height * Half)) - (e.Top + (e.Height * Half));
            offsets.Add(MathF.Sqrt((dx * dx) + (dy * dy)));
        }

        return new(expectedCount, actualCount, textMatches, Median(offsets));
    }

    /// <summary>Gets the median of a list of numbers.</summary>
    /// <param name="values">The numbers; sorted in place.</param>
    /// <returns>The median, or zero for an empty list.</returns>
    private static float Median(List<float> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        values.Sort();
        return values[(int)(values.Count * Half)];
    }

    /// <summary>The comparison of one page.</summary>
    /// <param name="ExpectedCount">PDFium's character count.</param>
    /// <param name="ActualCount">The managed character count.</param>
    /// <param name="TextMatches">Whether the page text is equal.</param>
    /// <param name="MedianOffset">The median distance between the engines' character box centres, in points.</param>
    private readonly record struct PageComparison(int ExpectedCount, int ActualCount, bool TextMatches, float MedianOffset);
}
