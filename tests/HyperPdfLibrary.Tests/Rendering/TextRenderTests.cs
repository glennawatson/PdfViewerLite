// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Renders text with a test font, to check text positioning and the font factory.</summary>
[NotInParallel]
public sealed class TextRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 6;

    /// <summary>The device column in the middle of the first glyph.</summary>
    private const int FirstGlyph = 20;

    /// <summary>The device column in the middle of the second glyph, one advance of 10 points later.</summary>
    private const int SecondGlyph = 30;

    /// <summary>The device column in the middle of the third glyph, after a TJ adjustment.</summary>
    private const int ThirdGlyph = 45;

    /// <summary>The device row in the middle of the glyphs.</summary>
    private const int GlyphRow = 80;

    /// <summary>The device row of a point above the glyphs.</summary>
    private const int AboveGlyphs = 40;

    /// <summary>Text shows glyphs from the font at the text position, advancing by their widths.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextAdvancesByGlyphWidths()
    {
        var image = RenderText("BT /F1 20 Tf 10 10 Td (AA) Tj ET");

        await Assert.That(image.IsNear(FirstGlyph, GlyphRow, Rgb.Black, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(SecondGlyph, GlyphRow, Rgb.Black, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(FirstGlyph, AboveGlyphs, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>A TJ adjustment moves the next glyph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ArrayAdjustmentsMoveTheTextPosition()
    {
        var image = RenderText("BT /F1 20 Tf 10 10 Td [(A) -1000 (A)] TJ ET");

        await Assert.That(image.IsNear(FirstGlyph, GlyphRow, Rgb.Black, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(ThirdGlyph, GlyphRow, Rgb.White, Tolerance)).IsFalse();
    }

    /// <summary>Invisible text render mode draws nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvisibleTextIsNotDrawn()
    {
        var image = RenderText("BT /F1 20 Tf 3 Tr 10 10 Td (A) Tj ET");

        await Assert.That(image.IsNear(FirstGlyph, GlyphRow, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>The clip render mode restricts later painting to the glyph shapes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClipModeClipsToGlyphs()
    {
        var image = RenderText("BT /F1 20 Tf 7 Tr 10 10 Td (A) Tj ET 0 1 0 rg 0 0 100 100 re f");

        await Assert.That(image.IsNear(FirstGlyph, GlyphRow, Rgb.Green255, Tolerance)).IsTrue();
        await Assert.That(image.IsNear(FirstGlyph, AboveGlyphs, Rgb.White, Tolerance)).IsTrue();
    }

    /// <summary>Text shows nothing when no font factory is set.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextWithoutAFactoryShowsNothing()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "BT /F1 20 Tf 10 10 Td (A) Tj ET" };
        pdf.Resources = $"/Font << /F1 {pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Test >>")} 0 R >>";
        var previous = PdfFont.Factory;
        PdfFont.Factory = null;
        try
        {
            using var page = new RenderTestPage(pdf.ToBytes());
            var image = page.RenderPage();

            await Assert.That(image.IsNear(FirstGlyph, GlyphRow, Rgb.White, Tolerance)).IsTrue();
        }
        finally
        {
            PdfFont.Factory = previous;
        }
    }

    /// <summary>Renders a page whose font /F1 is the square test font.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderText(string content)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = content };
        pdf.Resources = $"/Font << /F1 {pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Test >>")} 0 R >>";
        var previous = PdfFont.Factory;
        PdfFont.Factory = static dictionary => new SquareFont(dictionary);
        try
        {
            using var page = new RenderTestPage(pdf.ToBytes());
            return page.RenderPage();
        }
        finally
        {
            PdfFont.Factory = previous;
        }
    }
}
