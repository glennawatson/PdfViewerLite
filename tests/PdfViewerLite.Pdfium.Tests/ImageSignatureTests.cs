// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Printing;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Image signatures keep their ink and transparency after saving.</summary>
public sealed class ImageSignatureTests
{
    /// <summary>The pixel channels in BGRA.</summary>
    private const int Channels = 4;

    /// <summary>The width and height of the source image.</summary>
    private const int ImageSize = 2;

    /// <summary>The centre of the first image pixel after scaling.</summary>
    private const int FirstPixel = 10;

    /// <summary>The centre of the second image pixel after scaling.</summary>
    private const int SecondPixel = 30;

    /// <summary>The image contains opaque black, soft black, transparent paper and blue ink.</summary>
    private static readonly byte[] Ink = [0, 0, 0, 255, 0, 0, 0, 127, 0, 0, 0, 0, 255, 0, 0, 255];

    /// <summary>The same ink scanned on white paper.</summary>
    private static readonly byte[] Scan = [0, 0, 0, 255, 128, 128, 128, 255, 255, 255, 255, 255, 255, 0, 0, 255];

    /// <summary>The signature rectangle, away from the generated page's text.</summary>
    private static readonly PageRect Bounds = new(100, 300, 40, 40);

    /// <summary>Saving keeps image orientation and soft alpha edges in the PDF itself.</summary>
    /// <param name="removePaper">Whether the source needs paper removal.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SavesImageWithTransparency(bool removePaper)
    {
        using var source = new TestDocument(1);
        var image = (removePaper ? Scan : Ink).ToArray();
        if (removePaper)
        {
            SignaturePixels.RemoveWhitePaper(image);
        }

        var index = ((IImageSignatureEditor)DocumentFeatures.CastFeature(source.Document, typeof(IImageSignatureEditor))!).AddImageSignature(0, Bounds, image, ImageSize, ImageSize);
        await Assert.That(index).IsGreaterThanOrEqualTo(0);
        image.AsSpan().Clear();
        var exported = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-image-signature-{Guid.NewGuid():N}.pdf");
        try
        {
            await using (var stream = File.Create(exported))
            {
                await Assert.That(((IAnnotationEditor)DocumentFeatures.CastFeature(source.Document, typeof(IAnnotationEditor))!).Save(stream)).IsTrue();
            }

            using var copy = new PdfiumEngine().Open(exported, null);
            List<PageAnnotation> annotations = [];
            ((IAnnotationEditor)DocumentFeatures.CastFeature(copy, typeof(IAnnotationEditor))!).GetAnnotations(0, annotations);
            await Assert.That(annotations.Count).IsEqualTo(1);
            await Assert.That(annotations[0].Kind).IsEqualTo(AnnotationKind.Signature);
            await Assert.That(annotations[0].Bounds).IsEqualTo(Bounds);
            var pixels = new byte[(int)Bounds.Width * (int)Bounds.Height * Channels];
            var rendered = copy.Render(
                new(0, 1, PageRotation.None, (int)Bounds.Left, (int)Bounds.Top, RenderFlags.Annotations),
                new(pixels, (int)Bounds.Width, (int)Bounds.Height, (int)Bounds.Width * Channels));
            await Assert.That(rendered).IsTrue();
            await Assert.That(Pixel(pixels, FirstPixel, FirstPixel)).IsEqualTo(0xFF000000U);
            await Assert.That(Pixel(pixels, SecondPixel, FirstPixel)).IsEqualTo(0xFF808080U);
            await Assert.That(Pixel(pixels, FirstPixel, SecondPixel)).IsEqualTo(0xFFFFFFFFU);
            await Assert.That(Pixel(pixels, SecondPixel, SecondPixel)).IsEqualTo(0xFF0000FFU);
        }
        finally
        {
            File.Delete(exported);
        }
    }

    /// <summary>Incomplete images fail before the document is edited.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsMismatchedPixels()
    {
        using var source = new TestDocument(1);
        var editor = (IImageSignatureEditor)DocumentFeatures.CastFeature(source.Document, typeof(IImageSignatureEditor))!;
        await Assert.That(() => editor.AddImageSignature(0, Bounds, [], ImageSize, ImageSize)).Throws<ArgumentException>();
        await Assert.That(((IAnnotationEditor)DocumentFeatures.CastFeature(source.Document, typeof(IAnnotationEditor))!).HasUnsavedChanges).IsFalse();
    }

    /// <summary>Invalid image dimensions fail before native code is called.</summary>
    /// <param name="width">The invalid image width.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    public async Task RejectsInvalidWidth(int width)
    {
        using var source = new TestDocument(1);
        var editor = (IImageSignatureEditor)DocumentFeatures.CastFeature(source.Document, typeof(IImageSignatureEditor))!;
        await Assert.That(() => editor.AddImageSignature(0, Bounds, Ink, width, ImageSize)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(((IAnnotationEditor)DocumentFeatures.CastFeature(source.Document, typeof(IAnnotationEditor))!).HasUnsavedChanges).IsFalse();
    }

    /// <summary>A missing page cannot create a signature or mark the document as changed.</summary>
    /// <param name="page">The missing page.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(-1)]
    [Arguments(1)]
    public async Task RejectsMissingPage(int page)
    {
        using var source = new TestDocument(1);
        var index = ((IImageSignatureEditor)DocumentFeatures.CastFeature(source.Document, typeof(IImageSignatureEditor))!).AddImageSignature(page, Bounds, Ink, ImageSize, ImageSize);
        await Assert.That(index).IsEqualTo(-1);
        await Assert.That(((IAnnotationEditor)DocumentFeatures.CastFeature(source.Document, typeof(IAnnotationEditor))!).HasUnsavedChanges).IsFalse();
    }

    /// <summary>Image signatures use the same removal action as other signatures.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovesImageSignature()
    {
        using var source = new TestDocument(1);
        var index = ((IImageSignatureEditor)DocumentFeatures.CastFeature(source.Document, typeof(IImageSignatureEditor))!).AddImageSignature(0, Bounds, Ink, ImageSize, ImageSize);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(source.Document, typeof(IAnnotationEditor))!;
        List<PageAnnotation> annotations = [];
        editor.GetAnnotations(0, annotations);
        await Assert.That(annotations.Count).IsEqualTo(1);
        await Assert.That(editor.Remove(0, index)).IsTrue();
        annotations.Clear();
        editor.GetAnnotations(0, annotations);
        await Assert.That(annotations).IsEmpty();
    }

    /// <summary>Printed image signatures survive page fitting and sheet layouts.</summary>
    /// <param name="imposition">The sheet layout.</param>
    /// <param name="pagesPerSheet">The pages on each sheet.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PrintImposition.Pages, 1)]
    [Arguments(PrintImposition.Pages, 2)]
    [Arguments(PrintImposition.Booklet, 1)]
    [Arguments(PrintImposition.Poster, 1)]
    public async Task PrintsImageSignature(PrintImposition imposition, int pagesPerSheet)
    {
        using var source = new TestDocument(1);
        var index = ((IImageSignatureEditor)DocumentFeatures.CastFeature(source.Document, typeof(IImageSignatureEditor))!).AddImageSignature(0, Bounds, Ink, ImageSize, ImageSize);
        await Assert.That(index).IsGreaterThanOrEqualTo(0);
        var exported = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-image-print-{Guid.NewGuid():N}.pdf");
        try
        {
            await using (var stream = File.Create(exported))
            {
                var layout = new SheetLayout(pagesPerSheet, PaperSize.A4, true) { Imposition = imposition, PosterTiles = 1, FitToPaper = true };
                await Assert.That(((IPageExporter)DocumentFeatures.CastFeature(source.Document, typeof(IPageExporter))!).ExportPages([0], layout, stream)).IsTrue();
            }

            using var copy = new PdfiumEngine().Open(exported, null);
            await Assert.That(ContainsBlueInk(copy)).IsTrue();
        }
        finally
        {
            File.Delete(exported);
        }
    }

    /// <summary>Renders every sheet and checks for the signature's blue ink.</summary>
    /// <param name="document">The exported print copy.</param>
    /// <returns>Whether the image is visible in the page content.</returns>
    private static bool ContainsBlueInk(IDocument document)
    {
        const byte opaque = 255;
        const byte midpoint = 128;
        const int redOffset = 2;
        var sizes = document.GetPageSizes();
        for (var page = 0; page < sizes.Length; page++)
        {
            var width = (int)Math.Ceiling(sizes[page].Width);
            var height = (int)Math.Ceiling(sizes[page].Height);
            var pixels = new byte[width * height * Channels];
            if (!document.Render(new(page, 1, PageRotation.None, 0, 0, RenderFlags.Printing), new(pixels, width, height, width * Channels)))
            {
                continue;
            }

            for (var offset = 0; offset < pixels.Length; offset += Channels)
            {
                if (pixels[offset] == opaque && pixels[offset + 1] < midpoint && pixels[offset + redOffset] < midpoint)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Reads an output pixel as packed ARGB.</summary>
    /// <param name="pixels">The rendered signature rectangle.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The packed colour.</returns>
    private static uint Pixel(byte[] pixels, int x, int y) => BitConverter.ToUInt32(pixels, ((y * (int)Bounds.Width) + x) * Channels);
}
