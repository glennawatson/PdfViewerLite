// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using PdfViewerLite.Core.Signatures;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// Checks that the signature fields the validator reads match PDFium's <c>FPDFSignatureObj_*</c> reads on signed files
/// with later incremental updates. PDFium does not validate, so only the field data is compared.
/// </summary>
public sealed class SignatureValidationParityTests
{
    /// <summary>DocMDP P 2.</summary>
    private const int FormFill = 2;

    /// <summary>A certified, then filled-in and commented file lists the same signature as PDFium, and the validator reads that signature.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UpdatedCertifiedFileMatches()
    {
        using var certificate = PdfSigning.CreateCertificate("Parity Signer", null, false, TimeProvider.System);
        var file = SignatureSamples.AddAnnotation(SignatureSamples.FillForm(SignatureSamples.Signed(certificate, FormFill, string.Empty)));
        using var pair = new EnginePair(file);
        var expected = ((ISignatureSource)pair.Pdfium).GetSignatures();
        using var document = PdfDocument.Open(file, null);
        var details = document.GetSignatureDetails();

        await Assert.That(details.Count).IsEqualTo(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(details[i].Field.Contents).IsEquivalentTo(expected[i].Contents);
            await Assert.That(details[i].Field.ByteRange).IsEquivalentTo(expected[i].ByteRange);
            await Assert.That(details[i].Field.SubFilter).IsEqualTo(expected[i].SubFilter);
            await Assert.That(details[i].Field.Reason).IsEqualTo(expected[i].Reason);
            await Assert.That(details[i].Field.SigningTime).IsEqualTo(expected[i].SigningTime);
        }
    }

    /// <summary>An unsigned signature field is listed by both engines with empty contents.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsignedFieldMatches()
    {
        using var pair = new EnginePair(SignatureSamples.Unsigned());
        var expected = ((ISignatureSource)pair.Pdfium).GetSignatures();
        var actual = ((ISignatureSource)pair.HyperPdf).GetSignatures();

        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        await Assert.That(actual[0].Contents).IsEquivalentTo(expected[0].Contents);
        await Assert.That(actual[0].ByteRange).IsEquivalentTo(expected[0].ByteRange);
    }
}
