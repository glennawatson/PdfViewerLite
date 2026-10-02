// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Measuring;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Checks the scale a drawing declares in its page viewport is read for measuring.</summary>
public sealed class ViewportTests
{
    /// <summary>One inch on paper, in PDF points.</summary>
    private const double Inch = 72;

    /// <summary>The real length one inch stands for in the test drawing, in feet.</summary>
    private const double FeetPerInch = 10;

    /// <summary>The tolerance for comparing lengths.</summary>
    private const double Tolerance = 1e-6;

    /// <summary>A page's /VP measure dictionary gives the scale.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsTheDeclaredScale()
    {
        var scale = PdfViewports.ReadScale(TestPdf.CreateWithViewport(), 0);

        await Assert.That(scale).IsNotNull();
        await Assert.That(scale!.Value.ToString()).IsEqualTo(TestPdf.ViewportScale);
        await Assert.That(scale.Value.ToReal(Inch)).IsEqualTo(FeetPerInch).Within(Tolerance);
    }

    /// <summary>A page without a viewport declares no scale.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrdinaryPagesHaveNoScale() => await Assert.That(PdfViewports.ReadScale(TestPdf.Create(1), 0)).IsNull();

    /// <summary>Damaged input is not an error, just no scale.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnreadableFilesHaveNoScale() => await Assert.That(PdfViewports.ReadScale("not a pdf"u8.ToArray(), 0)).IsNull();
}
