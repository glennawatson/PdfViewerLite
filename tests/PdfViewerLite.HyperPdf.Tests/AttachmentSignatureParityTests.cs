// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Attachments;
using PdfViewerLite.Core.Signatures;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Checks that HyperPDF reads embedded files and signatures as PDFium does.</summary>
public sealed class AttachmentSignatureParityTests
{
    /// <summary>The pages in the signed sample.</summary>
    private const int Pages = 2;

    /// <summary>Attachments list with the same names and sizes, and save the same bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AttachmentsMatch()
    {
        using var pair = new EnginePair(TestPdf.CreateWithAttachment());
        var expected = ((IAttachmentSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(pair.Pdfium, typeof(IAttachmentSource))!).GetAttachments();
        var actual = ((IAttachmentSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(pair.HyperPdf, typeof(IAttachmentSource))!).GetAttachments();

        await Assert.That(actual).IsEquivalentTo(expected);
        await Assert.That(Save(pair.HyperPdf, 0)).IsEquivalentTo(Save(pair.Pdfium, 0));
    }

    /// <summary>Signatures list with the same contents, byte range, format, reason and time.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignaturesMatch()
    {
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        using var pair = new EnginePair(TestSignedPdf.Create(Pages, certificate));
        var expected = ((ISignatureSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(pair.Pdfium, typeof(ISignatureSource))!).GetSignatures();
        var actual = ((ISignatureSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(pair.HyperPdf, typeof(ISignatureSource))!).GetSignatures();

        await Assert.That(((ISignatureSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(
            pair.HyperPdf,
            typeof(ISignatureSource))!).SignatureCount).IsEqualTo(((ISignatureSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(pair.Pdfium, typeof(ISignatureSource))!).SignatureCount);
        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(actual[i].Contents).IsEquivalentTo(expected[i].Contents);
            await Assert.That(actual[i].ByteRange).IsEquivalentTo(expected[i].ByteRange);
            await Assert.That(actual[i].SubFilter).IsEqualTo(expected[i].SubFilter);
            await Assert.That(actual[i].Reason).IsEqualTo(expected[i].Reason);
            await Assert.That(actual[i].SigningTime).IsEqualTo(expected[i].SigningTime);
        }
    }

    /// <summary>Saves an attachment to memory.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The attachment index.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Save(Core.Documents.IDocument document, int index)
    {
        using var stream = new MemoryStream();
        _ = ((IAttachmentSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(IAttachmentSource))!).SaveAttachment(index, stream);
        return stream.ToArray();
    }
}
