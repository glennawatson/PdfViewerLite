// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests for <see cref="PdfiumDocument"/>.</summary>
public sealed class PdfiumDocumentTests
{
    /// <summary>The number of pages in the generated document.</summary>
    private const int PageCount = 5;

    /// <summary>The zero based index of the third page, the target of the internal link.</summary>
    private const int ThirdPage = 2;

    /// <summary>The zero based index of the fourth page.</summary>
    private const int FourthPage = 3;

    /// <summary>The left margin used by the generated document, in points.</summary>
    private const float Margin = 72F;

    /// <summary>Tolerance for positions, in points.</summary>
    private const float PositionTolerance = 1F;

    /// <summary>The bytes per BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The time zone offset in the generated creation date.</summary>
    private const int CreationOffsetHours = 10;

    /// <summary>Verifies page count and sizes, including the landscape page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsPageCountAndSizes()
    {
        using var test = new TestDocument(PageCount);
        var sizes = test.Document.GetPageSizes();

        await Assert.That(test.Document.PageCount).IsEqualTo(PageCount);
        await Assert.That(sizes.Length).IsEqualTo(PageCount);
        for (var i = 0; i < PageCount; i++)
        {
            TestPdf.GetPageSize(i, out var width, out var height);
            await Assert.That(sizes[i]).IsEqualTo(new(width, height));
        }
    }

    /// <summary>Verifies metadata is decoded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsMetadata()
    {
        using var test = new TestDocument(PageCount);
        var metadata = test.Document.GetMetadata();

        await Assert.That(metadata.Title).IsEqualTo(TestPdf.Title);
        await Assert.That(metadata.Author).IsEqualTo(TestPdf.Author);
        await Assert.That(metadata.FormatVersion).IsEqualTo("1.7");
        await Assert.That(metadata.IsEncrypted).IsFalse();
        await Assert.That(metadata.Created).IsEqualTo(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(CreationOffsetHours)));
    }

    /// <summary>Verifies the outline has an entry per page that targets that page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsOutline()
    {
        using var test = new TestDocument(PageCount);
        var outline = test.Document.GetOutline();

        await Assert.That(outline.Count).IsEqualTo(PageCount);
        for (var i = 0; i < PageCount; i++)
        {
            await Assert.That(outline[i].Title).IsEqualTo($"Chapter {i + 1}");
            await Assert.That(outline[i].Target.Kind).IsEqualTo(LinkTargetKind.Page);
            await Assert.That(outline[i].Target.PageIndex).IsEqualTo(i);
        }
    }

    /// <summary>Verifies page and URI links are found on page 1 at the expected position.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsLinks()
    {
        using var test = new TestDocument(PageCount);
        var links = test.Document.GetLinks(0);

        const float linkTopMin = 150F;
        const float linkTopMax = 200F;
        var pageLink = links.Single(static l => l.Target.Kind == LinkTargetKind.Page);
        await Assert.That(pageLink.Target.PageIndex).IsEqualTo(ThirdPage);
        await Assert.That(pageLink.Bounds.Left).IsEqualTo(Margin).Within(PositionTolerance);
        await Assert.That(pageLink.Bounds.Top).IsBetween(linkTopMin, linkTopMax);

        var uriLinks = links.Where(static l => l.Target.Kind == LinkTargetKind.Uri).ToList();
        await Assert.That(uriLinks.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(uriLinks.TrueForAll(static l => l.Target.Uri == TestPdf.LinkUri)).IsTrue();
    }

    /// <summary>Verifies text extraction and hit testing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtractsText()
    {
        using var test = new TestDocument(PageCount);
        var count = test.Document.GetCharacterCount(0);
        var text = test.Document.GetText(0, 0, count);

        await Assert.That(text).Contains("Page 1");
        await Assert.That(text).Contains(TestPdf.Sentence);

        const float headingX = 80F;
        const float headingY = 64F;
        const float tolerance = 4F;
        var index = test.Document.GetCharacterIndexAt(0, new(headingX, headingY), tolerance);
        await Assert.That(index).IsGreaterThanOrEqualTo(0);
        await Assert.That(test.Document.GetText(0, index, 1)).IsEqualTo("P");
    }

    /// <summary>Verifies search finds matches on the right page and reports their bounds in top-left page space.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsText()
    {
        using var test = new TestDocument(PageCount);
        var matches = new List<TextMatch>();
        const float markerTopMin = 100F;
        const float markerTopMax = 135F;
        test.Document.Find(FourthPage, "page4marker", SearchOptions.None, matches);

        await Assert.That(matches.Count).IsEqualTo(1);
        var bounds = new List<PageRect>();
        test.Document.GetTextBounds(FourthPage, matches[0].Start, matches[0].Length, bounds);
        await Assert.That(bounds.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(bounds[0].Top).IsBetween(markerTopMin, markerTopMax);

        matches.Clear();
        test.Document.Find(1, "QUICK", SearchOptions.MatchCase, matches);
        await Assert.That(matches.Count).IsEqualTo(0);
        test.Document.Find(1, "QUICK", SearchOptions.None, matches);
        await Assert.That(matches.Count).IsEqualTo(1);
    }

    /// <summary>Verifies rendering writes non-white pixels where the heading is and white elsewhere.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RendersTile()
    {
        using var test = new TestDocument(PageCount);
        const int width = 300;
        const int height = 200;
        const int stride = width * BytesPerPixel;
        const int minimumInk = 50;
        const int blankCorner = 60;
        var pixels = new byte[stride * height];
        var info = new PageRenderInfo(0, 1F, PageRotation.None, 0, 0, RenderFlags.Annotations);

        var rendered = test.Document.Render(info, new(pixels, width, height, stride));

        await Assert.That(rendered).IsTrue();
        await Assert.That(CountDarkPixels(pixels, stride, 0, 0, width, height)).IsGreaterThan(minimumInk);
        await Assert.That(CountDarkPixels(pixels, stride, 0, 0, blankCorner, blankCorner)).IsEqualTo(0);
    }

    /// <summary>Verifies rendering an offset tile places content relative to the tile origin.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RendersOffsetTile()
    {
        using var test = new TestDocument(PageCount);
        const int size = 64;
        const int stride = size * BytesPerPixel;
        const int offsetX = 500;
        const int offsetY = 700;
        var pixels = new byte[stride * size];

        // A tile far from any text must be blank.
        var info = new PageRenderInfo(0, 1F, PageRotation.None, offsetX, offsetY, RenderFlags.None);
        _ = test.Document.Render(info, new(pixels, size, size, stride));

        await Assert.That(CountDarkPixels(pixels, stride, 0, 0, size, size)).IsEqualTo(0);
    }

    /// <summary>Verifies calls on a disposed document fail gracefully.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposedDocumentIsSafe()
    {
        var test = new TestDocument(PageCount);
        test.Dispose();
        const int size = 2;
        const int stride = size * BytesPerPixel;
        var pixels = new byte[stride * size];

        await Assert.That(test.Document.IsDisposed).IsTrue();
        await Assert.That(test.Document.Render(new(0, 1F, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, size, size, stride))).IsFalse();
        await Assert.That(test.Document.GetCharacterCount(0)).IsEqualTo(0);
        await Assert.That(test.Document.GetLinks(0).Count).IsEqualTo(0);
    }

    /// <summary>Counts pixels darker than mid grey in a region.</summary>
    /// <param name="pixels">The BGRA pixels.</param>
    /// <param name="stride">The stride.</param>
    /// <param name="left">The region left.</param>
    /// <param name="top">The region top.</param>
    /// <param name="width">The region width.</param>
    /// <param name="height">The region height.</param>
    /// <returns>The count.</returns>
    private static int CountDarkPixels(byte[] pixels, int stride, int left, int top, int width, int height)
    {
        const int threshold = 128;
        var count = 0;
        for (var y = top; y < top + height; y++)
        {
            for (var x = left; x < left + width; x++)
            {
                if (pixels[(y * stride) + (x * BytesPerPixel)] < threshold)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
