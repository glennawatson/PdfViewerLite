// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Tests.Rendering;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Reading;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// Checks that the managed text pages give the characters, text, search results and web links PDFium gives. Until the
/// library loads real fonts, Helvetica is played by <see cref="StandInFont"/>, whose widths and text match PDFium's;
/// the geometry test needs real glyph outlines and is skipped until then.
/// </summary>
public sealed class TextParityTests
{
    /// <summary>The page width in points.</summary>
    private const int PageWidth = 612;

    /// <summary>The page height in points.</summary>
    private const int PageHeight = 792;

    /// <summary>The page content: several lines with spaces, moves, kerning, a hyphenated word and addresses.</summary>
    private const string Content =
        "BT /F1 12 Tf 72 700 Td (The quick brown fox jumps) Tj 0 -16 Td (over the lazy dog. Visit www.example.com) Tj "
        + "0 -16 Td [(Kerned) -400 (words) 30 (here)] TJ 0 -16 Td (A long hyphen-) Tj 0 -16 Td (ated word and mail me@example.org) Tj ET "
        + "BT /F1 12 Tf 72 600 Td (Left) Tj 100 0 Td (Right) Tj ET";

    /// <summary>
    /// Page content with the cases PDFium handles specially: /ActualText, text drawn twice for fake bold, invisible
    /// text, runs drawn right to left along a line and a duplicated glyph.
    /// </summary>
    private const string SpecialContent =
        "/Span /P1 BDC BT /F1 12 Tf 72 700 Td (xyz) Tj ET EMC "
        + "BT /F1 12 Tf 72 680 Td (Bold) Tj ET BT /F1 12 Tf 72.3 680 Td (Bold) Tj ET "
        + "BT /F1 12 Tf 3 Tr 72 660 Td (Hidden layer) Tj ET "
        + "BT /F1 12 Tf 200 640 Td (second) Tj ET BT /F1 12 Tf 72 640 Td (first) Tj ET "
        + "BT /F1 12 Tf 72 620 Td [(A) 600 (A) (B)] TJ ET";

    /// <summary>The property list resources of <see cref="SpecialContent"/>.</summary>
    private const string SpecialResources = "/Properties << /P1 << /ActualText (abc) >> >>";

    /// <summary>Half, for the centre of a rectangle.</summary>
    private const float Half = 0.5F;

    /// <summary>The largest difference allowed between the engines' boxes, in points.</summary>
    private const float BoxTolerance = 1;

    /// <summary>The reason the geometry test is skipped.</summary>
    private const string NeedsRealFonts = "Glyph boxes need the library's real fonts; the stand-in font has box outlines.";

    /// <summary>The queries searched for.</summary>
    private static readonly string[] Queries = ["the", "fox jumps", "WORD", "o", "example", "Left Right"];

    /// <summary>The search options tried with each query.</summary>
    private static readonly SearchOptions[] Options = [SearchOptions.None, SearchOptions.MatchCase, SearchOptions.WholeWord];

    /// <summary>Guards swapping the global font factory.</summary>
    private static readonly Lock FactoryGate = new();

    /// <summary>The character count and page text match PDFium's. Passes with the stand-in font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CountAndTextMatch()
    {
        using var pair = Open(out var document, out _);
        var count = pair.Pdfium.GetCharacterCount(0);

        await Assert.That(count).IsGreaterThan(0);
        await Assert.That(document.GetCharacterCountNative(0)).IsEqualTo(count);
        await Assert.That(document.GetTextNative(0, 0, count)).IsEqualTo(pair.Pdfium.GetText(0, 0, count));
    }

    /// <summary>/ActualText, fake bold, invisible text, run order and duplicate glyphs give PDFium's text. Passes with the stand-in font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpecialCasesMatch()
    {
        using var pair = Open(SpecialContent, SpecialResources, out var document, out _);
        var count = pair.Pdfium.GetCharacterCount(0);

        await Assert.That(document.GetCharacterCountNative(0)).IsEqualTo(count);
        await Assert.That(document.GetTextNative(0, 0, count)).IsEqualTo(pair.Pdfium.GetText(0, 0, count));
    }

    /// <summary>Each character's value and generated flag match PDFium's. Passes with the stand-in font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CharactersMatch()
    {
        using var pair = Open(out var document, out _);
        var expected = new List<PageCharacter>();
        ((ITextLayoutSource)pair.Pdfium).GetCharacters(0, expected);
        var actual = new List<PageCharacter>();
        document.GetCharactersNative(0, actual);

        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(actual[i].Value).IsEqualTo(expected[i].Value);
            await Assert.That(actual[i].Generated).IsEqualTo(expected[i].Generated);
        }
    }

    /// <summary>Search results match PDFium's for each option. Passes with the stand-in font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SearchMatches()
    {
        using var pair = Open(out var document, out _);
        foreach (var query in Queries)
        {
            foreach (var options in Options)
            {
                var expected = new List<TextMatch>();
                pair.Pdfium.Find(0, query, options, expected);
                var actual = new List<TextMatch>();
                document.FindNative(0, query, options, actual);

                await Assert.That(actual).IsEquivalentTo(expected);
            }
        }
    }

    /// <summary>The addresses written as text match PDFium's web links. Passes with the stand-in font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WebLinksMatch()
    {
        using var pair = Open(out var document, out _);
        var actual = new List<PageLink>();
        document.GetWebLinksNative(0, actual);

        await Assert.That(Uris(actual)).IsEquivalentTo(Uris(pair.Pdfium.GetLinks(0)));
    }

    /// <summary>Text bounds and hit testing match PDFium's. Needs the library's real fonts; skipped with the stand-in.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GeometryMatches()
    {
        using var pair = Open(out var document, out var standIn);
        if (standIn)
        {
            Skip.Test(NeedsRealFonts);
        }

        var count = pair.Pdfium.GetCharacterCount(0);
        var expected = new List<PageRect>();
        pair.Pdfium.GetTextBounds(0, 0, count, expected);
        var actual = new List<PageRect>();
        document.GetTextBoundsNative(0, 0, count, actual);

        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(actual[i].Left).IsEqualTo(expected[i].Left).Within(BoxTolerance);
            await Assert.That(actual[i].Top).IsEqualTo(expected[i].Top).Within(BoxTolerance);
        }

        foreach (var rect in expected)
        {
            var centre = new PagePoint(rect.Left + (rect.Width * Half), rect.Top + (rect.Height * Half));
            await Assert.That(document.GetCharacterIndexAtNative(0, centre, 0)).IsEqualTo(pair.Pdfium.GetCharacterIndexAt(0, centre, 0));
        }
    }

    /// <summary>Opens the main test page with both engines and extracts the managed text with the fonts in place.</summary>
    /// <param name="document">Receives the managed document.</param>
    /// <param name="standIn">Receives whether the stand-in font was used.</param>
    /// <returns>The pair, which deletes the file on dispose.</returns>
    private static EnginePair Open(out HyperPdfDocument document, out bool standIn) => Open(Content, string.Empty, out document, out standIn);

    /// <summary>Opens a page with both engines and extracts the managed text with the fonts in place.</summary>
    /// <param name="content">The page content; /F1 is Helvetica.</param>
    /// <param name="resources">More resource entries.</param>
    /// <param name="document">Receives the managed document.</param>
    /// <param name="standIn">Receives whether the stand-in font was used.</param>
    /// <returns>The pair, which deletes the file on dispose.</returns>
    private static EnginePair Open(string content, string resources, out HyperPdfDocument document, out bool standIn)
    {
        var pdf = new RenderTestPdf(PageWidth, PageHeight) { Content = content };
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/Font << /F1 {font} 0 R >> {resources}");
        var pair = new EnginePair(pdf.ToBytes());
        document = (HyperPdfDocument)pair.HyperPdf;
        standIn = LoadText(document);
        return pair;
    }

    /// <summary>
    /// Extracts the managed text once so it is cached, with the stand-in font for this document's fonts when the library
    /// has no font loader. The loader is restored straight away, and other documents never see the stand-in.
    /// </summary>
    /// <param name="document">The managed document.</param>
    /// <returns><see langword="true"/> when the stand-in font was used.</returns>
    private static bool LoadText(HyperPdfDocument document)
    {
        lock (FactoryGate)
        {
            var previous = PdfFont.Factory;
            if (previous is not null)
            {
                _ = document.GetCharacterCountNative(0);
                return false;
            }

            var owner = document.Document.Objects;
            PdfFont.Factory = dictionary => ReferenceEquals(dictionary.Owner, owner) ? StandInFont.Create(dictionary) : null;
            try
            {
                _ = document.GetCharacterCountNative(0);
            }
            finally
            {
                PdfFont.Factory = previous;
            }

            return true;
        }
    }

    /// <summary>Gets the web addresses of links.</summary>
    /// <param name="links">The links.</param>
    /// <returns>The distinct addresses, in order.</returns>
    private static List<string> Uris(IEnumerable<PageLink> links)
    {
        var uris = new List<string>();
        foreach (var link in links)
        {
            if (link.Target.Kind == LinkTargetKind.Uri && link.Target.Uri is { } uri && !uris.Contains(uri))
            {
                uris.Add(uri);
            }
        }

        return uris;
    }
}
