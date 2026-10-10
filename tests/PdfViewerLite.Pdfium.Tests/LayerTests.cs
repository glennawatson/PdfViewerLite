// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests for listing, showing and hiding layers through <see cref="ILayerSource"/>.</summary>
public sealed class LayerTests
{
    /// <summary>The layers in the test document.</summary>
    private const int LayerCount = 2;

    /// <summary>Verifies the layers are listed with the document's own visibility, and pages draw accordingly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsLayersAsTheDocumentSetsThem()
    {
        using var test = new LayerDocument();
        var layers = test.Layers.GetLayers();
        var (left, right) = test.SampleBoxes();

        await Assert.That(layers.Count).IsEqualTo(LayerCount);
        await Assert.That(layers[0].Name).IsEqualTo(TestPdf.DrawingLayer);
        await Assert.That(layers[0].IsVisible).IsTrue();
        await Assert.That(layers[1].Name).IsEqualTo(TestPdf.NotesLayer);
        await Assert.That(layers[1].IsVisible).IsFalse();
        await Assert.That(left).IsTrue();
        await Assert.That(right).IsFalse();
    }

    /// <summary>Verifies showing one layer and hiding the other changes what pages draw, and switching back restores them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsAndHidesLayers()
    {
        using var test = new LayerDocument();
        var layers = test.Layers.GetLayers();
        var shown = test.Layers.SetLayerVisible(layers[1].Id, true);
        var hidden = test.Layers.SetLayerVisible(layers[0].Id, false);
        var (leftAfter, rightAfter) = test.SampleBoxes();
        _ = test.Layers.SetLayerVisible(layers[0].Id, true);
        _ = test.Layers.SetLayerVisible(layers[1].Id, false);
        var (leftBack, rightBack) = test.SampleBoxes();

        await Assert.That(shown && hidden).IsTrue();
        await Assert.That(test.Layers.GetLayers()[0].IsVisible).IsTrue();
        await Assert.That(leftAfter).IsFalse();
        await Assert.That(rightAfter).IsTrue();
        await Assert.That(leftBack).IsTrue();
        await Assert.That(rightBack).IsFalse();
    }

    /// <summary>Verifies documents without layers list none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsNoneWithoutLayers()
    {
        var path = TestPdf.WriteTempFile(1);
        try
        {
            using var document = new PdfiumEngine().Open(path, null);

            await Assert.That(((ILayerSource)DocumentFeatures.CastFeature(document, typeof(ILayerSource))!).GetLayers().Count).IsEqualTo(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>The layered test document.</summary>
    private sealed class LayerDocument : IDisposable
    {
        /// <summary>The bytes in a BGRA pixel.</summary>
        private const int BytesPerPixel = 4;

        /// <summary>A channel value darker than this counts as the black box.</summary>
        private const byte Dark = 64;

        /// <summary>Half a box, to sample its middle.</summary>
        private const int HalfBox = 50;

        /// <summary>The file.</summary>
        private readonly string _path;

        /// <summary>The document.</summary>
        private readonly IDocument _document;

        /// <summary>Initializes a new instance of the <see cref="LayerDocument"/> class.</summary>
        public LayerDocument()
        {
            _path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-layers-{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(_path, TestPdf.CreateWithLayers());
            _document = new PdfiumEngine().Open(_path, null);
        }

        /// <summary>Gets the document's layers.</summary>
        public ILayerSource Layers => (ILayerSource)DocumentFeatures.CastFeature(_document, typeof(ILayerSource))!;

        /// <inheritdoc/>
        public void Dispose()
        {
            _document.Dispose();
            File.Delete(_path);
        }

        /// <summary>Renders the page at one pixel per point and tells whether each box is drawn.</summary>
        /// <returns>Whether the left and right boxes are dark.</returns>
        public (bool Left, bool Right) SampleBoxes()
        {
            var size = _document.GetPageSizes()[0];
            var width = (int)size.Width;
            var height = (int)size.Height;
            var pixels = new byte[width * height * BytesPerPixel];
            _ = _document.Render(new(0, 1, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, width, height, width * BytesPerPixel));
            var y = height - (TestPdf.LayerBoxBottom + HalfBox);
            return (IsDark(pixels, width, TestPdf.LayerBoxLeft + HalfBox, y), IsDark(pixels, width, TestPdf.LayerBoxRight + HalfBox, y));
        }

        /// <summary>Determines whether a pixel is dark.</summary>
        /// <param name="pixels">The pixels.</param>
        /// <param name="width">The width.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns><see langword="true"/> when dark.</returns>
        private static bool IsDark(byte[] pixels, int width, int x, int y) => pixels[((y * width) + x) * BytesPerPixel] < Dark;
    }
}
