// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Checks that HyperPDF answers document questions the way PDFium does.</summary>
public sealed class DocumentParityTests
{
    /// <summary>The pages in the sample document.</summary>
    private const int Pages = 4;

    /// <summary>Page sizes match PDFium's.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageSizesMatch()
    {
        using var pair = new EnginePair(TestPdf.Create(Pages));

        await Assert.That(pair.HyperPdf.GetPageSizes()).IsEquivalentTo(pair.Pdfium.GetPageSizes());
    }

    /// <summary>Metadata matches PDFium's.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MetadataMatches()
    {
        using var pair = new EnginePair(TestPdf.Create(Pages));

        await Assert.That(pair.HyperPdf.GetMetadata()).IsEqualTo(pair.Pdfium.GetMetadata());
    }

    /// <summary>The outline matches PDFium's titles and targets.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutlineMatches()
    {
        using var pair = new EnginePair(TestPdf.Create(Pages));
        var expected = pair.Pdfium.GetOutline();
        var actual = pair.HyperPdf.GetOutline();

        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(actual[i].Title).IsEqualTo(expected[i].Title);
            await Assert.That(actual[i].Target).IsEqualTo(expected[i].Target);
            await Assert.That(actual[i].IsOpen).IsEqualTo(expected[i].IsOpen);
        }
    }

    /// <summary>Link annotations and the web links found in the page text match PDFium's bounds, targets and order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinkAnnotationsMatch()
    {
        using var pair = new EnginePair(TestPdf.Create(Pages));
        var expected = pair.Pdfium.GetLinks(0);
        var actual = pair.HyperPdf.GetLinks(0);

        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(actual[i].Target).IsEqualTo(expected[i].Target);
            await Assert.That(Math.Abs(actual[i].Bounds.Left - expected[i].Bounds.Left)).IsLessThan(1F);
            await Assert.That(Math.Abs(actual[i].Bounds.Top - expected[i].Bounds.Top)).IsLessThan(1F);
        }
    }

    /// <summary>Layers and their visibility match PDFium's.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayersMatch()
    {
        using var pair = new EnginePair(TestPdf.CreateWithLayers());
        var expected = ((ILayerSource)DocumentFeatures.CastFeature(pair.Pdfium, typeof(ILayerSource))!).GetLayers();
        var actual = ((ILayerSource)DocumentFeatures.CastFeature(pair.HyperPdf, typeof(ILayerSource))!).GetLayers();

        await Assert.That(actual).IsEquivalentTo(expected);
    }

    /// <summary>Page labels match PDFium's, including documents without labels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageLabelsMatch()
    {
        using var pair = new EnginePair(TestPdf.Create(Pages));
        for (var i = 0; i < Pages; i++)
        {
            await Assert.That(pair.HyperPdf.GetPageLabel(i)).IsEqualTo(pair.Pdfium.GetPageLabel(i));
        }
    }
}
