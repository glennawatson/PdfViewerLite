// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Redaction;
using HyperPdfLibrary.Writing;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Reopens edited and redacted files with PDFium: pages, text and looks must match what HyperPDF says.</summary>
[NotInParallel]
public sealed class PageObjectPdfiumTests
{
    /// <summary>The mean channel difference allowed between the engines, out of 255.</summary>
    private const double EngineTolerance = 4;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The colour channels compared in a pixel.</summary>
    private const int ColorChannels = 3;

    /// <summary>The distance the first text object moves up, in points.</summary>
    private const float Lift = 40;

    /// <summary>A page whose first text object is deleted and whose second is moved reopens in PDFium, saved both ways.</summary>
    /// <param name="incremental">Whether the edit is saved as an incremental update.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EditedPageReopensInPdfium(bool incremental)
    {
        var source = TestPdf.Create(1);
        byte[] saved;
        string deleted;
        using (var document = PdfDocumentReader.Open(source, null))
        {
            var content = PdfDocumentPageContent.GetPageContent(document, 0);
            var texts = TextObjects(content);
            deleted = texts[0].Text;
            texts[0].Delete();
            texts[1].Translate(0, Lift);
            content.Apply();
            saved = incremental ? PdfIncrementalWriter.Save(document.Objects) : PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);
        }

        using var pair = new EnginePair(saved);
        var count = pair.Pdfium.GetCharacterCount(0);
        var pdfiumText = pair.Pdfium.GetText(0, 0, count);

        await Assert.That(pair.Pdfium.PageCount).IsEqualTo(pair.HyperPdf.PageCount);
        await Assert.That(pdfiumText).DoesNotContain(deleted);
        await Assert.That(pair.HyperPdf.GetText(0, 0, pair.HyperPdf.GetCharacterCount(0))).IsEqualTo(pdfiumText);
        await Assert.That(MeanDifference(new(pair.Pdfium, 0, RenderFlags.None), new(pair.HyperPdf, 0, RenderFlags.None))).IsLessThan(EngineTolerance);
    }

    /// <summary>A redacted file reopens in PDFium without the redacted text, and PDFium draws it as HyperPDF does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RedactedFileReopensInPdfium()
    {
        var source = TestPdf.Create(1);
        string secret;
        byte[] saved;
        using (var document = PdfDocumentReader.Open(source, null))
        {
            var target = TextObjects(PdfDocumentPageContent.GetPageContent(document, 0))[0];
            secret = target.Text;
            _ = PdfRedactions.Add(document, 0, [target.Bounds], PdfRedactionAppearance.Black);
            await using var output = new MemoryStream();
            _ = PdfRedactor.ApplyAndSave(document, output, PdfRedactionOptions.Default);
            saved = output.ToArray();
        }

        using var pair = new EnginePair(saved);
        var text = pair.Pdfium.GetText(0, 0, pair.Pdfium.GetCharacterCount(0));

        await Assert.That(text).DoesNotContain(secret);
        await Assert.That(text.Length).IsGreaterThan(0);
        await Assert.That(pair.HyperPdf.GetText(0, 0, pair.HyperPdf.GetCharacterCount(0))).IsEqualTo(text);
        await Assert.That(MeanDifference(new(pair.Pdfium, 0, RenderFlags.None), new(pair.HyperPdf, 0, RenderFlags.None))).IsLessThan(EngineTolerance);
    }

    /// <summary>Lists the text objects of a content.</summary>
    /// <param name="content">The content.</param>
    /// <returns>The text objects in painting order.</returns>
    private static List<PdfTextObject> TextObjects(PdfPageContent content)
    {
        var texts = new List<PdfTextObject>();
        foreach (var item in content.Objects)
        {
            if (item is PdfTextObject { GlyphCount: > 0 } text)
            {
                texts.Add(text);
            }
        }

        return texts;
    }

    /// <summary>Gets the mean difference between two renders, per colour channel.</summary>
    /// <param name="first">The first render.</param>
    /// <param name="second">The second render.</param>
    /// <returns>The mean absolute difference from 0 to 255.</returns>
    private static double MeanDifference(PagePixels first, PagePixels second)
    {
        long total = 0;
        var samples = 0L;
        for (var i = 0; i + BytesPerPixel <= first.Pixels.Length && i + BytesPerPixel <= second.Pixels.Length; i += BytesPerPixel)
        {
            for (var channel = 0; channel < ColorChannels; channel++)
            {
                total += Math.Abs(first.Pixels[i + channel] - second.Pixels[i + channel]);
                samples++;
            }
        }

        return samples == 0 ? 0 : (double)total / samples;
    }
}
