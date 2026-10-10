// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Reading;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Reading order on a real two-column PDF read through PDFium.</summary>
public sealed class ReadingOrderPdfTests
{
    /// <summary>How the right column starts.</summary>
    private const string RightStart = "The right";

    /// <summary>The article's pages.</summary>
    private const int Pages = 3;

    /// <summary>The title is read first, then the left column, the right column and the footnote; the header and page number are left out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsTheArticleInOrder()
    {
        var path = Path.Combine(Path.GetTempPath(), $"article-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateArticle(Pages));
        try
        {
            using var document = new PdfiumEngine().Open(path, null);
            var reading = new ReadingDocument((ITextLayoutSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(ITextLayoutSource))!, document.GetPageSizes());
            var first = reading.GetPage(0);
            var second = reading.GetPage(1);

            await Assert.That(first.Blocks.Select(static b => b.Text)).IsEquivalentTo(
            [
                "Reading Order in Practice",
                "The left column opens with a first paragraph that ends.",
                "A second left paragraph follows after a gap.",
                "The right column comes next and finishes here.",
                "1 A footnote about the source.",
            ]);
            await Assert.That(first.Blocks.Select(static b => b.Kind)).IsEquivalentTo(
                [ReadingBlockKind.Heading, ReadingBlockKind.Paragraph, ReadingBlockKind.Paragraph, ReadingBlockKind.Paragraph, ReadingBlockKind.Footnote]);
            await Assert.That(second.Blocks[0].Text).IsEqualTo("The left column opens with a first paragraph that ends.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Each block's characters map back to the page, so the sentence being read can be highlighted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MapsBackToPageCharacters()
    {
        var path = Path.Combine(Path.GetTempPath(), $"article-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateArticle(1));
        try
        {
            using var document = new PdfiumEngine().Open(path, null);
            var reading = new ReadingDocument((ITextLayoutSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(ITextLayoutSource))!, document.GetPageSizes());
            var block = reading.GetPage(0).Blocks.Single(static b => b.Text.StartsWith(RightStart, StringComparison.Ordinal));
            var first = block.CharIndices[0];

            await Assert.That(document.GetText(0, first, RightStart.Length)).IsEqualTo(RightStart);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
