// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Tests finding content the viewer cannot show or run, natively and against PDFium.</summary>
public sealed class ContentCheckTests
{
    /// <summary>A page index past the end of every sample.</summary>
    private const int MissingPage = 5;

    /// <summary>The sample documents.</summary>
    public enum Sample
    {
        /// <summary>A plain one page document.</summary>
        Plain = 0,

        /// <summary>A form with a text field, check box and combo box.</summary>
        Form = 1,

        /// <summary>A form whose scripts are all recognised.</summary>
        CalculatedForm = 2,

        /// <summary>A document using everything that cannot be shown.</summary>
        Unsupported = 3,

        /// <summary>A form with comb fields.</summary>
        CombForm = 4,

        /// <summary>A flat form.</summary>
        FlatForm = 5,

        /// <summary>A page with typewriter text.</summary>
        FreeText = 6,

        /// <summary>A tagged document.</summary>
        Tagged = 7,

        /// <summary>A document with layers.</summary>
        Layers = 8,

        /// <summary>A document with an attached file.</summary>
        Attachment = 9,
    }

    /// <summary>Verifies XFA forms, scripts, media players and 3D models are found, and a plain document has none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsUnsupportedContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-unsupported-{Guid.NewGuid():N}.pdf");
        var plain = TestPdf.WriteTempFile(1);
        await File.WriteAllBytesAsync(path, TestPdf.CreateWithUnsupportedContent());
        try
        {
            using var document = new HyperPdfEngine().Open(path, null);
            var check = (IContentCheck)DocumentFeatures.CastFeature(document, typeof(IContentCheck))!;
            await Assert.That(check.CheckDocument()).IsEqualTo(UnsupportedContent.XfaForm | UnsupportedContent.JavaScript);
            await Assert.That(check.CheckPage(0)).IsEqualTo(UnsupportedContent.Multimedia | UnsupportedContent.ThreeD | UnsupportedContent.JavaScript);
            await Assert.That(check.CheckPage(1)).IsEqualTo(UnsupportedContent.None);

            using var simple = new HyperPdfEngine().Open(plain, null);
            await Assert.That(((IContentCheck)DocumentFeatures.CastFeature(simple, typeof(IContentCheck))!).CheckDocument()).IsEqualTo(UnsupportedContent.None);
            await Assert.That(((IContentCheck)DocumentFeatures.CastFeature(simple, typeof(IContentCheck))!).CheckPage(0)).IsEqualTo(UnsupportedContent.None);
        }
        finally
        {
            File.Delete(path);
            File.Delete(plain);
        }
    }

    /// <summary>A form whose scripts are all ones that are run is not flagged for JavaScript.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecognisedFormScriptsAreNotFlagged()
    {
        using var pair = new EnginePair(TestPdf.CreateCalculatedForm());

        await Assert.That(((IContentCheck)DocumentFeatures.CastFeature(pair.HyperPdf, typeof(IContentCheck))!).CheckPage(0)).IsEqualTo(UnsupportedContent.None);
        await Assert.That(((IContentCheck)DocumentFeatures.CastFeature(
            pair.HyperPdf,
            typeof(IContentCheck))!).CheckDocument()).IsEqualTo(((IContentCheck)DocumentFeatures.CastFeature(pair.Pdfium, typeof(IContentCheck))!).CheckDocument());
    }

    /// <summary>Every sample document is judged the same by HyperPDF and PDFium.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Sample.Plain)]
    [Arguments(Sample.Form)]
    [Arguments(Sample.CalculatedForm)]
    [Arguments(Sample.Unsupported)]
    [Arguments(Sample.CombForm)]
    [Arguments(Sample.FlatForm)]
    [Arguments(Sample.FreeText)]
    [Arguments(Sample.Tagged)]
    [Arguments(Sample.Layers)]
    [Arguments(Sample.Attachment)]
    public async Task VerdictsMatchPdfium(Sample sample)
    {
        using var pair = new EnginePair(CreateSample(sample));
        var expected = (IContentCheck)DocumentFeatures.CastFeature(pair.Pdfium, typeof(IContentCheck))!;
        var actual = (IContentCheck)DocumentFeatures.CastFeature(pair.HyperPdf, typeof(IContentCheck))!;

        await Assert.That(actual.CheckDocument()).IsEqualTo(expected.CheckDocument());
        await Assert.That(actual.CheckPage(0)).IsEqualTo(expected.CheckPage(0));
        await Assert.That(actual.CheckPage(MissingPage)).IsEqualTo(expected.CheckPage(MissingPage));
        await Assert.That(actual.CheckPage(-1)).IsEqualTo(expected.CheckPage(-1));
    }

    /// <summary>Creates one of the sample documents.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateSample(Sample sample) => sample switch
    {
        Sample.Plain => TestPdf.Create(1),
        Sample.Form => TestPdf.CreateForm(),
        Sample.CalculatedForm => TestPdf.CreateCalculatedForm(),
        Sample.Unsupported => TestPdf.CreateWithUnsupportedContent(),
        Sample.CombForm => TestPdf.CreateCombForm(),
        Sample.FlatForm => TestPdf.CreateFlatForm(),
        Sample.FreeText => TestPdf.CreateWithFreeText(),
        Sample.Tagged => TestPdf.CreateTagged(),
        Sample.Layers => TestPdf.CreateWithLayers(),
        _ => TestPdf.CreateWithAttachment(),
    };
}
