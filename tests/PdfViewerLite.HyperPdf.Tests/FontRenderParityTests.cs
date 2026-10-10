// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Tests.Fonts;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Rendering;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Renders text pages with PDFium and with HyperPDF's managed fonts and compares the pixels.</summary>
[NotInParallel(nameof(FontRenderParityTests))]
public sealed class FontRenderParityTests
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>
    /// The largest mean absolute difference per channel accepted for embedded fonts, which both engines draw from the
    /// same outlines. PDFium snaps glyph origins to whole pixels and rasterises glyphs itself, so edges differ slightly.
    /// </summary>
    private const double EmbeddedThreshold = 1.5;

    /// <summary>
    /// The largest mean difference accepted for a standard font. Both engines draw it from the same bundled Foxit
    /// faces, so only rasterisation differs (measured 0.085).
    /// </summary>
    private const double SubstituteThreshold = 0.5;

    /// <summary>
    /// The broad corpus smoke-test limit. It detects severe render failures; per-page pixel differences are written
    /// to the test output and do not establish standards conformance.
    /// </summary>
    private const double CorpusThreshold = 200.0;

    /// <summary>The pages of each corpus file compared.</summary>
    private const int CorpusPages = 2;

    /// <summary>The render scale of corpus pages.</summary>
    private const float CorpusScale = 0.5F;

    /// <summary>The descriptor flags of a non-symbolic font.</summary>
    private const int Nonsymbolic = 32;

    /// <summary>The descriptor flags of a symbolic font.</summary>
    private const int Symbolic = 4;

    /// <summary>The text page content: two lines of mixed glyphs, spacing and a TJ adjustment.</summary>
    private const string SimpleText = "BT /F1 24 Tf 10 60 Td (Hello AVW mi) Tj 0 -30 Td 3 Tw [(a b) -500 (cd)] TJ ET";

    /// <summary>The composite text page content: CIDs of A, B and m.</summary>
    private const string CompositeText = "BT /F1 24 Tf 10 60 Td <00220023004E> Tj 0 -30 Td <0022> Tj ET";

    /// <summary>The variable naming another corpus folder.</summary>
    private const string CorpusVariable = "PDFVIEWERLITE_CORPUS_DIR";

    /// <summary>An embedded TrueType font matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task EmbeddedTrueTypeMatchesPdfium() => AssertParity(
        nameof(EmbeddedTrueTypeMatchesPdfium),
        FontTestDocument.Build(new() { Subtype = "TrueType", Program = TestFont.Create(), Flags = Nonsymbolic, Entries = "/Encoding /WinAnsiEncoding", Content = SimpleText }),
        EmbeddedThreshold);

    /// <summary>An embedded Type 1 font matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task EmbeddedType1MatchesPdfium()
    {
        var program = TestFontPrograms.Type1(out var length1);
        var spec = new FontSpec
        {
            Subtype = "Type1",
            Program = program,
            FileKey = "FontFile",
            FileEntries = string.Create(CultureInfo.InvariantCulture, $"/Length1 {length1}"),
            Flags = Symbolic,
            Content = SimpleText,
        };
        return AssertParity(nameof(EmbeddedType1MatchesPdfium), FontTestDocument.Build(spec), EmbeddedThreshold);
    }

    /// <summary>An embedded CFF font matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task EmbeddedCffMatchesPdfium() => AssertParity(
        nameof(EmbeddedCffMatchesPdfium),
        FontTestDocument.Build(new() { Subtype = "Type1", Program = TestFontPrograms.Cff(), FileKey = "FontFile3", FileEntries = "/Subtype /Type1C", Flags = Nonsymbolic, Content = SimpleText }),
        EmbeddedThreshold);

    /// <summary>An Identity-H CIDFontType2 font matches PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task EmbeddedCompositeMatchesPdfium() => AssertParity(
        nameof(EmbeddedCompositeMatchesPdfium),
        FontTestDocument.Build(new() { Subtype = "Type0", Program = TestFont.Create(), Flags = Symbolic, CidEntries = "/W [34 [500 500]]", Content = CompositeText }),
        EmbeddedThreshold);

    /// <summary>A Helvetica page, drawn with the bundled Foxit face, stays close to PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task StandardFontIsCloseToPdfium() =>
        AssertParity(nameof(StandardFontIsCloseToPdfium), TestPdf.Create(1), SubstituteThreshold);

    /// <summary>The first pages of each cached corpus document stay close to PDFium; skipped when the corpus is absent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CorpusPagesAreCloseToPdfium()
    {
        var folder = Environment.GetEnvironmentVariable(CorpusVariable) is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "corpus");
        var files = Directory.Exists(folder) ? Directory.GetFiles(folder, "*.pdf") : [];
        if (files.Length == 0)
        {
            Skip.Test($"No corpus PDFs in {folder}.");
        }

        var worst = 0.0;
        foreach (var file in files)
        {
            worst = Math.Max(worst, await CompareFileAsync(file));
        }

        await Assert.That(worst).IsLessThan(CorpusThreshold);
    }

    /// <summary>Compares the first pages of one file.</summary>
    /// <param name="file">The PDF path.</param>
    /// <returns>The largest mean difference.</returns>
    private static async Task<double> CompareFileAsync(string file)
    {
        using var pair = new EnginePair(await File.ReadAllBytesAsync(file));
        var worst = 0.0;
        var pages = Math.Min(CorpusPages, pair.Pdfium.PageCount);
        for (var page = 0; page < pages; page++)
        {
            var difference = Compare(pair, new(page, CorpusScale, PageRotation.None, 0, 0, RenderFlags.FixedDeviceColors), out _);
            TestContext.Current?.Output.WriteLine($"{Path.GetFileName(file)} page {page + 1}: mean difference {difference:F3}");
            worst = Math.Max(worst, difference);
        }

        return worst;
    }

    /// <summary>Renders a page with both engines and asserts the images are close.</summary>
    /// <param name="name">The test name.</param>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="threshold">The largest mean difference accepted.</param>
    /// <returns>A task.</returns>
    private static async Task AssertParity(string name, byte[] pdf, double threshold)
    {
        using var pair = new EnginePair(pdf);
        await pair.HyperPdf.PreparePageAsync(0, CancellationToken.None);
        var difference = Compare(pair, new(0, 1, PageRotation.None, 0, 0, RenderFlags.FixedDeviceColors), out var images);
        TestContext.Current?.Output.WriteLine($"{name}: mean difference {difference:F3}");
        if (difference >= threshold)
        {
            images.Save(name);
        }

        await Assert.That(difference).IsLessThan(threshold);
    }

    /// <summary>Renders a page with both engines.</summary>
    /// <param name="pair">The documents.</param>
    /// <param name="info">The page request.</param>
    /// <param name="images">The two renders.</param>
    /// <returns>The mean absolute difference per channel.</returns>
    /// <exception cref="InvalidOperationException">An engine could not render the page.</exception>
    private static double Compare(EnginePair pair, in PageRenderInfo info, out ParityImages images)
    {
        var size = pair.Pdfium.GetPageSizes()[info.PageIndex];
        TileGrid.GetPagePixelSize(size, info.Rotation, info.Scale, out var width, out var height);
        var expected = new byte[width * height * BytesPerPixel];
        var actual = new byte[expected.Length];
        var target = new RenderTarget(expected, width, height, width * BytesPerPixel);
        if (!pair.Pdfium.Render(info, target) || !pair.HyperPdf.Render(info, new(actual, width, height, width * BytesPerPixel)))
        {
            throw new InvalidOperationException("The page could not be rendered.");
        }

        images = new(expected, actual, width, height);
        long total = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            total += Math.Abs(expected[i] - actual[i]);
        }

        return (double)total / expected.Length;
    }
}
