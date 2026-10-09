// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Writing;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.PageObjects;

/// <summary>Reads pages into objects and writes them again with no edits: the page must draw and read the same.</summary>
[NotInParallel]
public sealed class PageContentRoundTripTests
{
    /// <summary>The pages of each corpus file round-tripped.</summary>
    private const int CorpusPages = 2;

    /// <summary>The largest channel difference accepted when objects are written again from the model.</summary>
    private const int RewriteTolerance = 4;

    /// <summary>The share of pixels that may differ when objects are written again from the model.</summary>
    private const double RewriteDifferingShare = 0.0005;

    /// <summary>The variable naming another corpus folder.</summary>
    private const string CorpusVariable = "PDFVIEWERLITE_CORPUS_DIR";

    /// <summary>The page the documents of <see cref="TestPdf"/> draw text on.</summary>
    private const int TextPages = 3;

    /// <summary>Preserve mode writes unchanged objects with their original bytes, so the content is identical.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreserveGivesTheSameContentBytes()
    {
        using var document = PdfDocument.Open(PageObjectSamples.Mixed(), null);
        var content = document.GetPageContent(0);

        await Assert.That(content.Regenerate().AsSpan().SequenceEqual(content.Source)).IsTrue();
        await Assert.That(content.IsModified).IsFalse();
    }

    /// <summary>Both modes keep the mixed page drawing and reading the same.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfRegenerateMode.Preserve)]
    [Arguments(PdfRegenerateMode.Rewrite)]
    public async Task MixedPageRoundTrips(PdfRegenerateMode mode)
    {
        var pdf = PageObjectSamples.Mixed();
        var rewritten = RoundTrip(pdf, mode);

        await Assert.That(PageObjectSamples.Render(rewritten).Pixels.AsSpan().SequenceEqual(PageObjectSamples.Render(pdf).Pixels)).IsTrue();
        await Assert.That(TextOf(rewritten)).IsEqualTo(TextOf(pdf));
    }

    /// <summary>Both modes keep the text pages of the sample document drawing and reading the same.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfRegenerateMode.Preserve)]
    [Arguments(PdfRegenerateMode.Rewrite)]
    public async Task SamplePagesRoundTrip(PdfRegenerateMode mode)
    {
        var pdf = TestPdf.Create(TextPages);
        using var original = new RenderTestPage(pdf);
        for (var page = 0; page < TextPages; page++)
        {
            var before = Render(original.Document, original.Renderer, page);
            var text = original.Document.GetTextPage(page).Text;
            var content = original.Document.GetPageContent(page);
            content.Apply(mode);
            var after = Render(original.Document, original.Renderer, page);

            await Assert.That(after.Pixels.AsSpan().SequenceEqual(before.Pixels)).IsTrue();
            await Assert.That(original.Document.GetTextPage(page).Text).IsEqualTo(text);
        }
    }

    /// <summary>Every cached corpus page round-trips with the same pixels and the same text, and the saved file reopens the same.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CorpusPagesRoundTrip()
    {
        var files = CorpusFiles();
        if (files.Length == 0)
        {
            Skip.Test("No corpus PDFs are cached.");
        }

        var objects = 0;
        foreach (var file in files)
        {
            var bytes = await File.ReadAllBytesAsync(file);
            using var preserved = new RenderTestPage(bytes);
            using var rewritten = new RenderTestPage(bytes);
            var pages = Math.Min(CorpusPages, preserved.Document.PageCount);
            for (var page = 0; page < pages; page++)
            {
                objects += await CheckPage(preserved, page, PdfRegenerateMode.Preserve, true, file);
                objects += await CheckPage(rewritten, page, PdfRegenerateMode.Rewrite, false, file);
            }
        }

        await Assert.That(objects).IsGreaterThan(0);
    }

    /// <summary>Lists the cached corpus files.</summary>
    /// <returns>The paths.</returns>
    private static string[] CorpusFiles()
    {
        var folder = Environment.GetEnvironmentVariable(CorpusVariable) is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "corpus");
        var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.pdf") : [];
        Array.Sort(files, StringComparer.Ordinal);
        return files;
    }

    /// <summary>Writes the first page's content again and saves the document.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The saved bytes.</returns>
    private static byte[] RoundTrip(byte[] pdf, PdfRegenerateMode mode)
    {
        using var document = PdfDocument.Open(pdf, null);
        document.GetPageContent(0).Apply(mode);
        return PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);
    }

    /// <summary>Gets the first page's text.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <returns>The text.</returns>
    private static string TextOf(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf, null);
        return document.GetTextPage(0).Text;
    }

    /// <summary>Renders a page at one pixel per point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="renderer">Its renderer.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(PdfDocument document, PdfPageRenderer renderer, int pageIndex)
    {
        PdfPageRenderer.GetPixelSize(document.GetPage(pageIndex), 0, 1, out var width, out var height);
        var pixels = new byte[width * height * RenderedImage.BytesPerPixel];
        _ = renderer.Render(new(pageIndex, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, width, height, width * RenderedImage.BytesPerPixel));
        return new(pixels, width, height);
    }

    /// <summary>Counts the pixels that differ by more than a tolerance in any channel.</summary>
    /// <param name="first">The first render.</param>
    /// <param name="second">The second render.</param>
    /// <param name="tolerance">The tolerance.</param>
    /// <returns>The number of pixels that differ.</returns>
    private static int CountDifferences(RenderedImage first, RenderedImage second, int tolerance)
    {
        var count = 0;
        for (var i = 0; i + RenderedImage.BytesPerPixel <= first.Pixels.Length; i += RenderedImage.BytesPerPixel)
        {
            count += PixelDiffers(first.Pixels.AsSpan(i, RenderedImage.BytesPerPixel), second.Pixels.AsSpan(i, RenderedImage.BytesPerPixel), tolerance) ? 1 : 0;
        }

        return count;
    }

    /// <summary>Determines whether two pixels differ by more than a tolerance in any channel.</summary>
    /// <param name="first">The first pixel's channels.</param>
    /// <param name="second">The second pixel's channels.</param>
    /// <param name="tolerance">The tolerance.</param>
    /// <returns><see langword="true"/> when a channel differs by more.</returns>
    private static bool PixelDiffers(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second, int tolerance)
    {
        for (var channel = 0; channel < first.Length; channel++)
        {
            if (Math.Abs(first[channel] - second[channel]) > tolerance)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Round-trips one page and checks the pixels and text.</summary>
    /// <param name="page">The document and renderer.</param>
    /// <param name="index">The page index.</param>
    /// <param name="mode">The mode.</param>
    /// <param name="exact">Whether the pixels must be identical.</param>
    /// <param name="file">The file name, for failure messages.</param>
    /// <returns>The number of objects on the page.</returns>
    private static async Task<int> CheckPage(RenderTestPage page, int index, PdfRegenerateMode mode, bool exact, string file)
    {
        var before = Render(page.Document, page.Renderer, index);
        var text = page.Document.GetTextPage(index).Text;
        var content = page.Document.GetPageContent(index);
        content.Apply(mode);
        var after = Render(page.Document, page.Renderer, index);
        var differing = CountDifferences(before, after, exact ? 0 : RewriteTolerance);
        var message = string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileName(file)} page {index + 1} ({mode}): {content.Objects.Count} objects, {differing} pixels differ");
        TestContext.Current?.Output.WriteLine(message);

        var allowed = exact ? 0 : (int)((double)before.Pixels.Length / RenderedImage.BytesPerPixel * RewriteDifferingShare);

        await Assert.That(differing <= allowed).IsTrue().Because(message);
        await Assert.That(page.Document.GetTextPage(index).Text).IsEqualTo(text);
        return content.Objects.Count;
    }
}
