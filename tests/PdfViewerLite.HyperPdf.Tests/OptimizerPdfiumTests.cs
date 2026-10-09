// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Tests.Writing;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Rendering;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Reopens optimised files with PDFium: pages, text and looks must match PDFium's reading of the original.</summary>
[NotInParallel]
public sealed class OptimizerPdfiumTests
{
    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The mean channel difference allowed for lossless changes, out of 255.</summary>
    private const double LosslessTolerance = 0.5;

    /// <summary>The mean channel difference allowed for lossy image changes, out of 255.</summary>
    private const double LossyTolerance = 6;

    /// <summary>The scan's width.</summary>
    private const int ScanWidth = 400;

    /// <summary>The scan's height.</summary>
    private const int ScanHeight = 520;

    /// <summary>The photo's width and height in pixels: drawn across a 612 point page, about 300 ppi.</summary>
    private const int PhotoPixels = 2400;

    /// <summary>The stroke width of the black-and-white scan.</summary>
    private const int Stroke = 3;

    /// <summary>The stroke spacing of the black-and-white scan.</summary>
    private const int Spacing = 13;

    /// <summary>The highest sample.</summary>
    private const int MaxSample = 255;

    /// <summary>The period of the photo's waves.</summary>
    private const double Wave = 37.0;

    /// <summary>The strength of the photo's waves.</summary>
    private const double WaveStrength = 60.0;

    /// <summary>The pages of the text sample.</summary>
    private const int PageCount = 3;

    /// <summary>The first char code given a width.</summary>
    private const int FirstChar = 32;

    /// <summary>The last char code given a width.</summary>
    private const int LastChar = 126;

    /// <summary>The documents tried, by name.</summary>
    public enum Sample
    {
        /// <summary>Text pages with an outline.</summary>
        Text = 0,

        /// <summary>A black-and-white scan, re-encoded as CCITT Group 4.</summary>
        BilevelScan = 1,

        /// <summary>A 300 ppi greyscale photo, downsampled and saved as JPEG.</summary>
        Photo = 2,

        /// <summary>A tagged document.</summary>
        Tagged = 3,

        /// <summary>A fillable form.</summary>
        Form = 4,

        /// <summary>A page in an embedded TrueType font, subset.</summary>
        EmbeddedFont = 5,

        /// <summary>An encrypted document.</summary>
        Encrypted = 6,
    }

    /// <summary>PDFium reads the optimised file with the same pages and text, and draws it the same within the tolerance.</summary>
    /// <param name="sample">The document.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Sample.Text)]
    [Arguments(Sample.BilevelScan)]
    [Arguments(Sample.Photo)]
    [Arguments(Sample.Tagged)]
    [Arguments(Sample.Form)]
    [Arguments(Sample.EmbeddedFont)]
    [Arguments(Sample.Encrypted)]
    public async Task PdfiumReadsOptimisedFiles(Sample sample)
    {
        var source = Build(sample);
        byte[] optimised;
        using (var document = PdfDocument.Open(source, null))
        {
            await using var output = new MemoryStream();
            _ = PdfOptimizer.Optimize(document, output, PdfOptimizeOptions.Balanced with { Language = "en" });
            optimised = output.ToArray();
        }

        using var before = new EnginePair(source);
        using var after = new EnginePair(optimised);
        var count = before.Pdfium.GetCharacterCount(0);
        var tolerance = sample == Sample.Photo ? LossyTolerance : LosslessTolerance;

        await Assert.That(after.Pdfium.PageCount).IsEqualTo(before.Pdfium.PageCount);
        await Assert.That(after.Pdfium.GetCharacterCount(0)).IsEqualTo(count);
        await Assert.That(after.Pdfium.GetText(0, 0, count)).IsEqualTo(before.Pdfium.GetText(0, 0, count));
        await Assert.That(MeanDifference(Render(before.Pdfium), Render(after.Pdfium))).IsLessThan(tolerance);
    }

    /// <summary>Builds a sample.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>The PDF.</returns>
    private static byte[] Build(Sample sample) => sample switch
    {
        Sample.Text => TestPdf.Create(PageCount),
        Sample.BilevelScan => TestPdf.CreateScan(Strokes(), ScanWidth, ScanHeight),
        Sample.Photo => TestPdf.CreateScan(Photo(), PhotoPixels, PhotoPixels),
        Sample.Tagged => TestPdf.CreateTagged(),
        Sample.Form => TestPdf.CreateForm(),
        Sample.EmbeddedFont => EmbeddedFont(),
        _ => WritingTestDocuments.Encrypt(TestPdf.Create(PageCount)),
    };

    /// <summary>Makes black strokes on white.</summary>
    /// <returns>The grey samples.</returns>
    private static byte[] Strokes()
    {
        var samples = new byte[ScanWidth * ScanHeight];
        for (var y = 0; y < ScanHeight; y++)
        {
            for (var x = 0; x < ScanWidth; x++)
            {
                samples[(y * ScanWidth) + x] = (x + (y / Stroke)) % Spacing < Stroke ? (byte)0 : (byte)MaxSample;
            }
        }

        return samples;
    }

    /// <summary>Makes a smooth greyscale picture.</summary>
    /// <returns>The grey samples.</returns>
    private static byte[] Photo()
    {
        var samples = new byte[PhotoPixels * PhotoPixels];
        for (var y = 0; y < PhotoPixels; y++)
        {
            for (var x = 0; x < PhotoPixels; x++)
            {
                var value = ((double)(x + y) * MaxSample / (PhotoPixels + PhotoPixels)) + (Math.Sin(x / Wave) * Math.Cos(y / Wave) * WaveStrength);
                samples[(y * PhotoPixels) + x] = (byte)Math.Clamp((int)value, 0, MaxSample);
            }
        }

        return samples;
    }

    /// <summary>Builds a page that shows text in the embedded test TrueType font.</summary>
    /// <returns>The PDF.</returns>
    private static byte[] EmbeddedFont()
    {
        var font = TestFont.Create();
        var widths = new StringBuilder();
        for (var code = FirstChar; code <= LastChar; code++)
        {
            _ = widths.Append(CultureInfo.InvariantCulture, $"{TestFont.AdvanceOf((char)code)} ");
        }

        return MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, "BT /F1 24 Tf 72 700 Td (Hello World) Tj ET\n"),
            string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Font /Subtype /TrueType /BaseFont /PVLTestSans /FirstChar {FirstChar} /LastChar {LastChar} /Widths [{widths}] /Encoding /WinAnsiEncoding /FontDescriptor 6 0 R >>"),
            "<< /Type /FontDescriptor /FontName /PVLTestSans /Flags 32 /FontBBox [0 -200 1000 800] /ItalicAngle 0 /Ascent 800 /Descent -200 /CapHeight 700 /StemV 80 /FontFile2 7 0 R >>",
            MiniPdf.Stream(string.Create(CultureInfo.InvariantCulture, $"/Length1 {font.Length}"), Encoding.Latin1.GetString(font)));
    }

    /// <summary>Renders the first page with an engine at one pixel per point.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The BGRA pixels.</returns>
    /// <exception cref="InvalidOperationException">The page could not be rendered.</exception>
    private static byte[] Render(IDocument document)
    {
        var size = document.GetPageSizes()[0];
        TileGrid.GetPagePixelSize(size, PageRotation.None, 1, out var width, out var height);
        var pixels = new byte[width * height * BytesPerPixel];
        return document.Render(new(0, 1, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, width, height, width * BytesPerPixel))
            ? pixels
            : throw new InvalidOperationException("The page could not be rendered.");
    }

    /// <summary>Computes the mean absolute difference per channel.</summary>
    /// <param name="expected">The reference pixels.</param>
    /// <param name="actual">The pixels under test.</param>
    /// <returns>The mean difference, from 0 to 255.</returns>
    private static double MeanDifference(byte[] expected, byte[] actual)
    {
        long total = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            total += Math.Abs(expected[i] - actual[i]);
        }

        return (double)total / expected.Length;
    }
}
