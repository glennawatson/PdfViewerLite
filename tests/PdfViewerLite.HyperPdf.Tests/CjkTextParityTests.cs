// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Reading;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Extracts text from pages in non-embedded CJK fonts with predefined CMaps and compares it with PDFium's.</summary>
public sealed class CjkTextParityTests
{
    /// <summary>A Shift-JIS page through 90ms-RKSJ-H gives PDFium's text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task ShiftJisMatches() => AssertParity(CjkParityPdf.Build("90ms-RKSJ-H", "Japan1", CjkParityPdf.ShiftJisContent, CjkParityPdf.HiraganaAWidth));

    /// <summary>A GB 2312 page through GB-EUC-H gives PDFium's text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task GbEucMatches() => AssertParity(CjkParityPdf.Build("GB-EUC-H", "GB1", CjkParityPdf.GbEucContent, string.Empty));

    /// <summary>A UCS-2 page through UniKS-UCS2-H gives PDFium's text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task UniKsUcs2Matches() => AssertParity(CjkParityPdf.Build("UniKS-UCS2-H", "Korea1", CjkParityPdf.UniKsContent, string.Empty));

    /// <summary>An Identity-H page of the Adobe-Japan1 collection without /ToUnicode gives PDFium's text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task IdentityJapan1Matches() => AssertParity(CjkParityPdf.Build("Identity-H", "Japan1", CjkParityPdf.IdentityJapan1Content, string.Empty));

    /// <summary>Opens a PDF with both engines and compares the character count, text and character values.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <returns>A task.</returns>
    private static async Task AssertParity(byte[] pdf)
    {
        using var pair = new EnginePair(pdf);
        var document = (HyperPdfDocument)pair.HyperPdf;
        await PdfViewerLite.HyperPdf.HyperPdfRendering.PreparePageAsync(document, 0, CancellationToken.None);
        var count = pair.Pdfium.GetCharacterCount(0);
        var expected = new List<PageCharacter>();
        ((ITextLayoutSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(pair.Pdfium, typeof(ITextLayoutSource))!).GetCharacters(0, expected);
        var actual = new List<PageCharacter>();
        PdfViewerLite.HyperPdf.HyperPdfText.GetCharactersNative(document, 0, actual);

        await Assert.That(count).IsGreaterThan(0);
        await Assert.That(PdfViewerLite.HyperPdf.HyperPdfText.GetCharacterCountNative(document, 0)).IsEqualTo(count);
        await Assert.That(PdfViewerLite.HyperPdf.HyperPdfText.GetTextNative(document, 0, 0, count)).IsEqualTo(pair.Pdfium.GetText(0, 0, count));
        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(actual[i].Value).IsEqualTo(expected[i].Value);
        }
    }
}
