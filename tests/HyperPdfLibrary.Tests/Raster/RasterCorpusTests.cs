// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Raster;

/// <summary>
/// Reads scanned court reports from the corpus cache. The expectations come from reading the files: the Library of
/// Congress files hold one 1824 by 2720 JBIG2 image per page at 300 dpi, with a hidden OCR layer and no PDF/R marker.
/// The tests do nothing when a file is not cached.
/// </summary>
[NotInParallel]
public sealed class RasterCorpusTests
{
    /// <summary>The Library of Congress page width in samples.</summary>
    private const int LocWidth = 1824;

    /// <summary>The Library of Congress page height in samples.</summary>
    private const int LocHeight = 2720;

    /// <summary>The Library of Congress scan resolution.</summary>
    private const float LocDpi = 300F;

    /// <summary>The images on each Internet Archive page: background and mask layer.</summary>
    private const int LayersPerPage = 2;

    /// <summary>The tolerance for the resolution.</summary>
    private const float DpiTolerance = 0.5F;

    /// <summary>The Library of Congress files named by the issue.</summary>
    private static readonly string[] LocIds =
    [
        "loc-brown-v-board", "loc-plessy-v-ferguson", "loc-marbury-v-madison", "loc-korematsu-v-united-states", "loc-gideon-v-wainwright",
    ];

    /// <summary>The Library of Congress files are raster pages with JBIG2 filters and an OCR layer, and make no claim.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LibraryOfCongressFilesAreJbig2RasterPages()
    {
        foreach (var id in LocIds)
        {
            if (Open(id) is not { } document)
            {
                continue;
            }

            using (document)
            {
                var report = PdfDocumentRaster.GetRasterReport(document, CancellationToken.None);
                var image = report.Pages[0].Images[0];

                await Assert.That(report.Claim).IsNull();
                await Assert.That(report.IsRasterOnly).IsTrue();
                await Assert.That(report.UsesAllowedFilters).IsTrue();
                await Assert.That(report.FiltersSeen).IsEquivalentTo(["JBIG2Decode"]);
                await Assert.That(report.Pages[0].HasOcrText).IsTrue();
                await Assert.That(report.Pages[0].OcrCharCount).IsGreaterThan(0);
                await Assert.That(image.Width).IsEqualTo(LocWidth);
                await Assert.That(image.Height).IsEqualTo(LocHeight);
                await Assert.That(image.ColorSpace).IsEqualTo("DeviceGray");
                await Assert.That(image.HorizontalDpi).IsEqualTo(LocDpi).Within(DpiTolerance);
            }
        }
    }

    /// <summary>The Internet Archive volume pairs a JPEG 2000 background with a mask layer, both RGB, on every page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InternetArchiveVolumeIsJpxRasterPages()
    {
        if (Open("ia-us-reports-228") is not { } document)
        {
            return;
        }

        using (document)
        {
            var report = PdfDocumentRaster.GetRasterReport(document, CancellationToken.None);

            await Assert.That(report.IsRasterOnly).IsTrue();
            await Assert.That(report.FiltersSeen).IsEquivalentTo(["JPXDecode"]);
            await Assert.That(report.Pages[0].Images.Count).IsEqualTo(LayersPerPage);
            await Assert.That(report.Pages[0].Images[0].HorizontalDpi).IsEqualTo(LocDpi).Within(DpiTolerance);
        }
    }

    /// <summary>The Google Books volume has a visible-text cover, so it is not raster-only; its body pages are JBIG2 at 600 dpi.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GoogleBooksCoverBreaksTheProfileButBodyPagesPass()
    {
        if (Open("google-reports-black-16") is not { } document)
        {
            return;
        }

        using (document)
        {
            var report = PdfDocumentRaster.GetRasterReport(document, CancellationToken.None);
            const float bodyDpi = 600F;

            await Assert.That(report.IsRasterOnly).IsFalse();
            await Assert.That(report.GetNonRasterPages()).IsEquivalentTo([0]);
            await Assert.That(report.Pages[1].IsRasterOnly).IsTrue();
            await Assert.That(report.Pages[1].Images[0].HorizontalDpi).IsEqualTo(bodyDpi).Within(DpiTolerance);
        }
    }

    /// <summary>Opens a cached corpus file.</summary>
    /// <param name="id">The corpus id.</param>
    /// <returns>The document, or null when the file is not cached.</returns>
    private static PdfDocument? Open(string id)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "corpus", $"{id}.pdf");
        return File.Exists(path) ? PdfDocumentReader.Open(path, null) : null;
    }
}
