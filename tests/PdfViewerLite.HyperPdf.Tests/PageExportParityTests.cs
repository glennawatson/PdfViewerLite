// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Printing;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Exports the same pages through PDFium and HyperPDF, then compares both outputs as PDFium reads them.</summary>
public sealed class PageExportParityTests
{
    /// <summary>The pages in the sample document.</summary>
    private const int Pages = 4;

    /// <summary>The most a page size may differ, in points.</summary>
    private const float SizeTolerance = 0.5F;

    /// <summary>The most a text position may differ, in points.</summary>
    private const float PositionTolerance = 1.5F;

    /// <summary>Half the true size, as a percentage.</summary>
    private const int HalfScale = 50;

    /// <summary>The number of poster tiles across.</summary>
    private const int PosterTiles = 2;

    /// <summary>Two pages on a sheet.</summary>
    private const int TwoPerSheet = 2;

    /// <summary>Four pages on a sheet.</summary>
    private const int FourPerSheet = 4;

    /// <summary>Six pages on a sheet.</summary>
    private const int SixPerSheet = 6;

    /// <summary>Nine pages on a sheet.</summary>
    private const int NinePerSheet = 9;

    /// <summary>The third page of the sample.</summary>
    private const int ThirdPage = 2;

    /// <summary>The layouts compared.</summary>
    public enum LayoutKind
    {
        /// <summary>One page per sheet, copied as it is.</summary>
        Plain = 0,

        /// <summary>Two to a sheet on A4.</summary>
        TwoUp = 1,

        /// <summary>Four to a sheet on Letter.</summary>
        FourUp = 2,

        /// <summary>Six to a sheet on A4.</summary>
        SixUp = 3,

        /// <summary>Each page fitted to A4.</summary>
        Fitted = 4,

        /// <summary>Each page at half size on Letter.</summary>
        HalfSize = 5,

        /// <summary>A booklet.</summary>
        Booklet = 6,

        /// <summary>A poster two sheets across.</summary>
        Poster = 7,

        /// <summary>Nine to a sheet on A3.</summary>
        NineUp = 8,
    }

    /// <summary>Page counts and sizes match PDFium for each layout.</summary>
    /// <param name="kind">The layout.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(LayoutKind.Plain)]
    [Arguments(LayoutKind.TwoUp)]
    [Arguments(LayoutKind.FourUp)]
    [Arguments(LayoutKind.SixUp)]
    [Arguments(LayoutKind.Fitted)]
    [Arguments(LayoutKind.HalfSize)]
    [Arguments(LayoutKind.Booklet)]
    [Arguments(LayoutKind.Poster)]
    [Arguments(LayoutKind.NineUp)]
    public async Task PageCountsAndSizesMatch(LayoutKind kind)
    {
        using var pair = ExportBoth(kind, out var source);
        try
        {
            await Assert.That(pair.Hyper.Written).IsEqualTo(pair.Pdfium.Written);
            await Assert.That(pair.Hyper.Output!.PageCount).IsEqualTo(pair.Pdfium.Output!.PageCount);
            var expected = pair.Pdfium.Output.GetPageSizes();
            var actual = pair.Hyper.Output.GetPageSizes();
            for (var i = 0; i < expected.Length; i++)
            {
                await Assert.That(actual[i].Width).IsEqualTo(expected[i].Width).Within(SizeTolerance);
                await Assert.That(actual[i].Height).IsEqualTo(expected[i].Height).Within(SizeTolerance);
            }
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>The text of each page, and where it sits, matches PDFium for each layout.</summary>
    /// <param name="kind">The layout.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(LayoutKind.Plain)]
    [Arguments(LayoutKind.TwoUp)]
    [Arguments(LayoutKind.FourUp)]
    [Arguments(LayoutKind.SixUp)]
    [Arguments(LayoutKind.Fitted)]
    [Arguments(LayoutKind.HalfSize)]
    [Arguments(LayoutKind.Booklet)]
    [Arguments(LayoutKind.Poster)]
    [Arguments(LayoutKind.NineUp)]
    public async Task TextAndPositionsMatch(LayoutKind kind)
    {
        using var pair = ExportBoth(kind, out var source);
        try
        {
            var expected = pair.Pdfium.Output!;
            var actual = pair.Hyper.Output!;
            for (var page = 0; page < expected.PageCount; page++)
            {
                var count = expected.GetCharacterCount(page);
                await Assert.That(actual.GetCharacterCount(page)).IsEqualTo(count);
                await Assert.That(actual.GetText(page, 0, count)).IsEqualTo(expected.GetText(page, 0, count));
                await AssertSameBounds(expected, actual, page, count);
            }
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>Creates a layout.</summary>
    /// <param name="kind">The layout's kind.</param>
    /// <returns>The layout.</returns>
    private static SheetLayout CreateLayout(LayoutKind kind) => kind switch
    {
        LayoutKind.Plain => SheetLayout.Default,
        LayoutKind.TwoUp => new(TwoPerSheet, PaperSize.A4, true),
        LayoutKind.FourUp => new(FourPerSheet, PaperSize.Letter, true),
        LayoutKind.SixUp => new(SixPerSheet, PaperSize.A4, true),
        LayoutKind.Fitted => new(1, PaperSize.A4, true) { FitToPaper = true },
        LayoutKind.HalfSize => new(1, PaperSize.Letter, true) { FitToPaper = true, Scaling = PrintScaling.Custom, ScalePercent = HalfScale },
        LayoutKind.Booklet => SheetLayout.Default with { Imposition = PrintImposition.Booklet },
        LayoutKind.Poster => SheetLayout.Default with { Imposition = PrintImposition.Poster, PosterTiles = PosterTiles },
        _ => new(NinePerSheet, PaperSize.A3, false),
    };

    /// <summary>Checks that the first character of a page sits in the same place in both documents.</summary>
    /// <param name="expected">PDFium's output.</param>
    /// <param name="actual">HyperPDF's output.</param>
    /// <param name="page">The page.</param>
    /// <param name="count">The characters on the page.</param>
    /// <returns>A task.</returns>
    private static async Task AssertSameBounds(IDocument expected, IDocument actual, int page, int count)
    {
        List<PageRect> expectedBounds = [];
        List<PageRect> actualBounds = [];
        if (count > 0)
        {
            expected.GetTextBounds(page, 0, 1, expectedBounds);
            actual.GetTextBounds(page, 0, 1, actualBounds);
        }

        await Assert.That(actualBounds.Count).IsEqualTo(expectedBounds.Count);
        for (var i = 0; i < expectedBounds.Count; i++)
        {
            await Assert.That(actualBounds[i].Left).IsEqualTo(expectedBounds[i].Left).Within(PositionTolerance);
            await Assert.That(actualBounds[i].Top).IsEqualTo(expectedBounds[i].Top).Within(PositionTolerance);
            await Assert.That(actualBounds[i].Width).IsEqualTo(expectedBounds[i].Width).Within(PositionTolerance);
        }
    }

    /// <summary>Exports the pages of the sample through both engines.</summary>
    /// <param name="kind">The layout.</param>
    /// <param name="source">The source file, which the caller deletes.</param>
    /// <returns>Both exports.</returns>
    private static ExportPair ExportBoth(LayoutKind kind, out string source)
    {
        source = TestPdf.WriteTempFile(Pages);
        var layout = CreateLayout(kind);
        int[] pages = [0, 1, ThirdPage, Pages - 1];
        return new(ExportRun.Create(source, pages, layout, false), ExportRun.Create(source, pages, layout, true));
    }

    /// <summary>The same export through both engines.</summary>
    private sealed class ExportPair : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ExportPair"/> class.</summary>
        /// <param name="pdfium">The PDFium export.</param>
        /// <param name="hyper">The HyperPDF export.</param>
        internal ExportPair(ExportRun pdfium, ExportRun hyper)
        {
            Pdfium = pdfium;
            Hyper = hyper;
        }

        /// <summary>Gets the PDFium export.</summary>
        internal ExportRun Pdfium { get; }

        /// <summary>Gets the HyperPDF export.</summary>
        internal ExportRun Hyper { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Pdfium.Dispose();
            Hyper.Dispose();
        }
    }
}
