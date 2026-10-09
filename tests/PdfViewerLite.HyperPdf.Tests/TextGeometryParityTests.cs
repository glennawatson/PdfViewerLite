// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Tests.Fonts;
using HyperPdfLibrary.Tests.Rendering;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// Checks that the managed text pages, built with the library's real fonts, give PDFium's character boxes, line
/// rectangles, hit testing and search result rectangles for each kind of font and text state.
/// </summary>
public sealed class TextGeometryParityTests
{
    /// <summary>The descriptor flags of a non-symbolic font.</summary>
    private const int Nonsymbolic = 32;

    /// <summary>The descriptor flags of a symbolic font.</summary>
    private const int Symbolic = 4;

    /// <summary>The /Subtype of Type 1 and CFF fonts.</summary>
    private const string Type1Subtype = "Type1";

    /// <summary>The /Encoding entry of the simple fonts.</summary>
    private const string WinAnsi = "/Encoding /WinAnsiEncoding";

    /// <summary>The descriptor metrics of the rotated page's font, as <see cref="FontTestDocument"/> writes them.</summary>
    private const string DescriptorMetrics = "/FontBBox [0 -200 1000 900] /ItalicAngle 0 /Ascent 800 /Descent -200 /CapHeight 700 /StemV 80";

    /// <summary>The largest difference allowed between the engines' boxes, in points.</summary>
    private const float BoxTolerance = 1;

    /// <summary>
    /// The largest difference allowed for a standard font, in points. The engines draw it with different faces, so
    /// outline boxes differ by the faces' design; advances and line positions still match.
    /// </summary>
    private const float SubstituteTolerance = 1.5F;

    /// <summary>Half, for the centre of a rectangle.</summary>
    private const float Half = 0.5F;

    /// <summary>The smallest width or height of a box whose centre is hit tested.</summary>
    private const float MinimumHitSize = 0.5F;

    /// <summary>The page width of the rotated page test.</summary>
    private const int RotatedWidth = 300;

    /// <summary>The page height of the rotated page test.</summary>
    private const int RotatedHeight = 200;

    /// <summary>Text of the embedded TrueType font: spaces, narrow and wide glyphs and a TJ adjustment.</summary>
    private const string TrueTypeText = "BT /F1 12 Tf 10 60 Td (Hello AVW mi) Tj 0 -20 Td [(a b) -500 (cd)] TJ ET";

    /// <summary>Text of the Type 1 and CFF fonts, which have only A and B.</summary>
    private const string RectangleText = "BT /F1 12 Tf 10 60 Td (ABBA) Tj 0 -20 Td [(AB) -800 (BA)] TJ ET";

    /// <summary>Text with horizontal scaling, rise, character spacing and word spacing.</summary>
    private const string SpacingText = "BT /F1 10 Tf 5 70 Td 140 Tz 3 Ts 1.5 Tc 6 Tw (Hi mi AV) Tj 0 -25 Td 80 Tz -2 Ts 0 Tc (low wide) Tj ET";

    /// <summary>Text with TJ kerning that pulls glyphs together, pushes them apart and opens spaces.</summary>
    private const string KerningText = "BT /F1 12 Tf 5 60 Td [(A) 120 (V) -80 (W) -1200 (mi) 300 (lk) -2500 (end)] TJ ET";

    /// <summary>Text on a rotated text matrix.</summary>
    private const string RotatedMatrixText = "BT /F1 12 Tf 0.866 0.5 -0.5 0.866 40 20 Tm (Tilted text) Tj ET";

    /// <summary>The Identity-H text: the glyph ids of A, B, m, space and a.</summary>
    private const string CompositeText = "BT /F1 14 Tf 10 60 Td <002200230001004E00420001> Tj 0 -20 Td [<0022> -400 <0023>] TJ ET";

    /// <summary>The vertical text: the glyph ids of A, B, m and a, written down the page.</summary>
    private const string VerticalText = "BT /F1 12 Tf 60 90 Td <00220023004E> Tj 30 0 Td [<0042> -300 <0022>] TJ ET";

    /// <summary>The standard font text.</summary>
    private const string StandardText =
        "BT /F1 11 Tf 6 80 Td (Quick brown fox, jumps!) Tj 0 -14 Td [(Ke) 40 (rn) -300 (ed)] TJ 0 -14 Td (Wavy Type: 0123) Tj ET";

    /// <summary>The CID-to-Unicode map of the Identity-H tests.</summary>
    private const string CompositeToUnicode =
        "/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CMapName /T def 1 begincodespacerange <0000> <FFFF> endcodespacerange "
        + "5 beginbfchar <0001> <0020> <0022> <0041> <0023> <0042> <0042> <0061> <004E> <006D> endbfchar endcmap CMapName currentdict /CMap defineresource pop end end";

    /// <summary>The /W widths of the composite tests: A and B at 500, a at 500, m at 800 and the space at 250.</summary>
    private const string CompositeWidths = "/W [1 [250] 34 [500 500] 66 [500] 78 [800]]";

    /// <summary>The queries searched for.</summary>
    private static readonly string[] Queries = ["a", "AB", "mi", "B A", "o", "end", "e"];

    /// <summary>An embedded TrueType font gives PDFium's geometry.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task EmbeddedTrueTypeMatches() => AssertGeometry(TrueType(TrueTypeText), BoxTolerance);

    /// <summary>An embedded Type 1 font gives PDFium's geometry.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task EmbeddedType1Matches()
    {
        var program = TestFontPrograms.Type1(out var length1);
        var spec = new FontSpec
        {
            Subtype = Type1Subtype,
            Program = program,
            FileKey = "FontFile",
            FileEntries = string.Create(CultureInfo.InvariantCulture, $"/Length1 {length1}"),
            Flags = Symbolic,
            Content = RectangleText,
        };
        return AssertGeometry(FontTestDocument.Build(spec), BoxTolerance);
    }

    /// <summary>An embedded CFF font gives PDFium's geometry.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task EmbeddedCffMatches()
    {
        var spec = new FontSpec { Subtype = Type1Subtype, Program = TestFontPrograms.Cff(), FileKey = "FontFile3", FileEntries = "/Subtype /Type1C", Flags = Nonsymbolic, Content = RectangleText };
        return AssertGeometry(FontTestDocument.Build(spec), BoxTolerance);
    }

    /// <summary>An Identity-H CID font gives PDFium's geometry.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task IdentityCompositeMatches() => AssertGeometry(Composite("/Identity-H", CompositeText), BoxTolerance);

    /// <summary>Vertical writing (WMode 1) gives PDFium's geometry.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task VerticalTextMatches() => AssertGeometry(Composite("/Identity-V", VerticalText), BoxTolerance);

    /// <summary>A standard font drawn with a substitute system face gives PDFium's geometry.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task StandardFontMatches() => AssertGeometry(
        FontTestDocument.Build(new() { Subtype = Type1Subtype, BaseFont = "Helvetica", Flags = -1, Entries = WinAnsi, Content = StandardText }),
        SubstituteTolerance);

    /// <summary>A standard serif font drawn with a substitute system face gives PDFium's geometry.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task StandardSerifFontMatches() => AssertGeometry(
        FontTestDocument.Build(new() { Subtype = Type1Subtype, BaseFont = "Times-Roman", Flags = -1, Entries = WinAnsi, Content = StandardText }),
        SubstituteTolerance);

    /// <summary>Horizontal scaling, rise, character spacing and word spacing give PDFium's geometry.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task TextStateMatches() => AssertGeometry(TrueType(SpacingText), BoxTolerance);

    /// <summary>TJ kerning gives PDFium's geometry and generated spaces.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task KerningMatches() => AssertGeometry(TrueType(KerningText), BoxTolerance);

    /// <summary>A rotated text matrix gives PDFium's geometry.</summary>
    /// <returns>A task.</returns>
    [Test]
    public Task RotatedTextMatrixMatches() => AssertGeometry(TrueType(RotatedMatrixText), BoxTolerance);

    /// <summary>Each page rotation gives PDFium's geometry in viewer space.</summary>
    /// <param name="rotate">The page /Rotate.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(90)]
    [Arguments(180)]
    [Arguments(270)]
    public Task RotatedPageMatches(int rotate) => AssertGeometry(RotatedTrueType(rotate, TrueTypeText), BoxTolerance);

    /// <summary>Builds a page whose /F1 is the embedded TrueType test font.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] TrueType(string content) =>
        FontTestDocument.Build(new() { Subtype = "TrueType", Program = TestFont.Create(), Flags = Nonsymbolic, Entries = WinAnsi, Content = content });

    /// <summary>Builds a page whose /F1 is a CIDFontType2 font over the TrueType test font.</summary>
    /// <param name="encoding">The CMap name.</param>
    /// <param name="content">The page content.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Composite(string encoding, string content) => FontTestDocument.Build(new()
    {
        Subtype = "Type0",
        Program = TestFont.Create(),
        Flags = Symbolic,
        CidEntries = CompositeWidths,
        CidEncoding = encoding,
        ToUnicode = CompositeToUnicode,
        Content = content,
    });

    /// <summary>Builds a rotated page whose /F1 is the embedded TrueType test font.</summary>
    /// <param name="rotate">The page /Rotate.</param>
    /// <param name="content">The page content.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] RotatedTrueType(int rotate, string content)
    {
        var pdf = new RenderTestPdf(RotatedWidth, RotatedHeight) { Content = content, PageEntries = string.Create(CultureInfo.InvariantCulture, $"/Rotate {rotate}") };
        var program = pdf.AddStream(string.Empty, TestFont.Create());
        var descriptor = pdf.AddObject(string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /FontDescriptor /FontName /Test /Flags {Nonsymbolic} {DescriptorMetrics} /FontFile2 {program} 0 R >>"));
        var font = pdf.AddObject(string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /Font /Subtype /TrueType /BaseFont /Test {WinAnsi} /FontDescriptor {descriptor} 0 R >>"));
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/Font << /F1 {font} 0 R >>");
        return pdf.ToBytes();
    }

    /// <summary>Opens a page with both engines and compares every geometry query.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="tolerance">The largest box difference allowed, in points.</param>
    /// <returns>A task.</returns>
    private static async Task AssertGeometry(byte[] pdf, float tolerance)
    {
        using var pair = new EnginePair(pdf);
        var document = (HyperPdfDocument)pair.HyperPdf;
        var count = pair.Pdfium.GetCharacterCount(0);

        await Assert.That(count).IsGreaterThan(0);
        await Assert.That(document.GetCharacterCountNative(0)).IsEqualTo(count);
        await Assert.That(document.GetTextNative(0, 0, count)).IsEqualTo(pair.Pdfium.GetText(0, 0, count));
        await AssertCharactersMatch(pair, document, tolerance);
        await AssertRectsMatch(Bounds(pair.Pdfium, 0, count), BoundsNative(document, 0, count), tolerance);
        await AssertHitsMatch(pair, document, count);
        await AssertSearchMatches(pair, document, tolerance);
    }

    /// <summary>Asserts each character's value, generated flag and box match PDFium's.</summary>
    /// <param name="pair">The documents.</param>
    /// <param name="document">The managed document.</param>
    /// <param name="tolerance">The largest box difference allowed.</param>
    /// <returns>A task.</returns>
    private static async Task AssertCharactersMatch(EnginePair pair, HyperPdfDocument document, float tolerance)
    {
        var expected = new List<PageCharacter>();
        ((ITextLayoutSource)pair.Pdfium).GetCharacters(0, expected);
        var actual = new List<PageCharacter>();
        document.GetCharactersNative(0, actual);

        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(actual[i].Value).IsEqualTo(expected[i].Value);
            await Assert.That(actual[i].Generated).IsEqualTo(expected[i].Generated);
            if (!expected[i].Generated)
            {
                await AssertRectMatches(expected[i].Bounds, actual[i].Bounds, tolerance);
            }
        }
    }

    /// <summary>Asserts the character found at the centre of each of PDFium's character boxes matches PDFium's.</summary>
    /// <param name="pair">The documents.</param>
    /// <param name="document">The managed document.</param>
    /// <param name="count">The character count.</param>
    /// <returns>A task.</returns>
    private static async Task AssertHitsMatch(EnginePair pair, HyperPdfDocument document, int count)
    {
        for (var i = 0; i < count; i++)
        {
            foreach (var rect in Bounds(pair.Pdfium, i, 1))
            {
                if (rect.Width < MinimumHitSize || rect.Height < MinimumHitSize)
                {
                    continue;
                }

                var centre = new PagePoint(rect.Left + (rect.Width * Half), rect.Top + (rect.Height * Half));
                await Assert.That(document.GetCharacterIndexAtNative(0, centre, 0)).IsEqualTo(pair.Pdfium.GetCharacterIndexAt(0, centre, 0));
            }
        }
    }

    /// <summary>Asserts search results and their rectangles match PDFium's.</summary>
    /// <param name="pair">The documents.</param>
    /// <param name="document">The managed document.</param>
    /// <param name="tolerance">The largest box difference allowed.</param>
    /// <returns>A task.</returns>
    private static async Task AssertSearchMatches(EnginePair pair, HyperPdfDocument document, float tolerance)
    {
        foreach (var query in Queries)
        {
            var expected = new List<TextMatch>();
            pair.Pdfium.Find(0, query, SearchOptions.None, expected);
            var actual = new List<TextMatch>();
            document.FindNative(0, query, SearchOptions.None, actual);

            await Assert.That(actual).IsEquivalentTo(expected);
            foreach (var match in expected)
            {
                await AssertRectsMatch(Bounds(pair.Pdfium, match.Start, match.Length), BoundsNative(document, match.Start, match.Length), tolerance);
            }
        }
    }

    /// <summary>Asserts two rectangle lists match.</summary>
    /// <param name="expected">PDFium's rectangles.</param>
    /// <param name="actual">The managed rectangles.</param>
    /// <param name="tolerance">The largest difference allowed.</param>
    /// <returns>A task.</returns>
    private static async Task AssertRectsMatch(List<PageRect> expected, List<PageRect> actual, float tolerance)
    {
        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            await AssertRectMatches(expected[i], actual[i], tolerance);
        }
    }

    /// <summary>Asserts two rectangles match.</summary>
    /// <param name="expected">PDFium's rectangle.</param>
    /// <param name="actual">The managed rectangle.</param>
    /// <param name="tolerance">The largest difference allowed.</param>
    /// <returns>A task.</returns>
    private static async Task AssertRectMatches(PageRect expected, PageRect actual, float tolerance)
    {
        await Assert.That(actual.Left).IsEqualTo(expected.Left).Within(tolerance);
        await Assert.That(actual.Top).IsEqualTo(expected.Top).Within(tolerance);
        await Assert.That(actual.Right).IsEqualTo(expected.Right).Within(tolerance);
        await Assert.That(actual.Bottom).IsEqualTo(expected.Bottom).Within(tolerance);
    }

    /// <summary>Gets PDFium's rectangles of a run of characters.</summary>
    /// <param name="document">The PDFium document.</param>
    /// <param name="start">The first character.</param>
    /// <param name="count">The number of characters.</param>
    /// <returns>The rectangles.</returns>
    private static List<PageRect> Bounds(IDocument document, int start, int count)
    {
        var rects = new List<PageRect>();
        document.GetTextBounds(0, start, count, rects);
        return rects;
    }

    /// <summary>Gets the managed rectangles of a run of characters.</summary>
    /// <param name="document">The managed document.</param>
    /// <param name="start">The first character.</param>
    /// <param name="count">The number of characters.</param>
    /// <returns>The rectangles.</returns>
    private static List<PageRect> BoundsNative(HyperPdfDocument document, int start, int count)
    {
        var rects = new List<PageRect>();
        document.GetTextBoundsNative(0, start, count, rects);
        return rects;
    }
}
