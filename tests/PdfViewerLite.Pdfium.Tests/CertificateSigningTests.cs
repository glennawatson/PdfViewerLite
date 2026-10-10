// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Signatures;
using PdfViewerLite.Core.Signatures.Signing;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests for signing documents with a certificate through <see cref="PdfSigner"/>.</summary>
public sealed class CertificateSigningTests
{
    /// <summary>The pages in the generated document.</summary>
    private const int Pages = 3;

    /// <summary>The signatures on a signed document signed again.</summary>
    private const int SignatureCountAfterCountersigning = 2;

    /// <summary>The reason recorded.</summary>
    private const string Reason = "Reviewed and approved";

    /// <summary>Where the note is added.</summary>
    private static readonly PagePoint NoteAt = new(100, 100);

    /// <summary>Verifies documents with classic and compressed structures sign, read back, and check as intact and trusted.</summary>
    /// <param name="compressed">Whether to use the compressed (object stream, cross-reference stream) layout.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SignsAndVerifies(bool compressed)
    {
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var source = compressed ? TestPdf.CreateCompressed() : TestPdf.Create(Pages);
        var result = await SignAndCheckAsync(source, certificate, 0);

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Integrity).IsEqualTo(SignatureIntegrity.Intact);
        await Assert.That(result[0].IsTrusted).IsTrue();
        await Assert.That(result[0].SignerName).IsEqualTo(TestSignedPdf.SignerName);
        await Assert.That(result[0].Reason).IsEqualTo(Reason);
    }

    /// <summary>Verifies a document PDFium saved with a new annotation signs on a later page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignsPdfiumSavedDocument()
    {
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var path = TestPdf.WriteTempFile(Pages);
        try
        {
            byte[] saved;
            using (var document = new PdfiumEngine().Open(path, null))
            {
                _ = ((IAnnotationEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(IAnnotationEditor))!).AddNote(1, NoteAt, "Before signing", AnnotationColors.Sand);
                await using var stream = new MemoryStream();
                _ = ((IAnnotationEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(IAnnotationEditor))!).Save(stream);
                saved = stream.ToArray();
            }

            var result = await SignAndCheckAsync(saved, certificate, Pages - 1);

            await Assert.That(result.Count).IsEqualTo(1);
            await Assert.That(result[0].Integrity).IsEqualTo(SignatureIntegrity.Intact);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies signing a signed document keeps the first signature valid for what it covered, and adds a second.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CountersignsSignedDocument()
    {
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var result = await SignAndCheckAsync(TestSignedPdf.Create(Pages, certificate), certificate, 0);

        await Assert.That(result.Count).IsEqualTo(SignatureCountAfterCountersigning);
        await Assert.That(result[0].Integrity).IsEqualTo(SignatureIntegrity.ChangedAfterSigning);
        await Assert.That(result[1].Integrity).IsEqualTo(SignatureIntegrity.Intact);
    }

    /// <summary>Verifies a certificate without a private key is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesCertificateWithoutKey()
    {
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        using var publicOnly = X509CertificateLoader.LoadCertificate(certificate.RawData);

        await Assert.That(() => PdfSigner.Sign(TestPdf.Create(1), publicOnly, new(0, Reason, string.Empty, TimeProvider.System.GetUtcNow()))).Throws<ArgumentException>();
    }

    /// <summary>Signs a document, then reads and checks its signatures with PDFium and the verifier.</summary>
    /// <param name="source">The document.</param>
    /// <param name="certificate">The certificate, also trusted for the check.</param>
    /// <param name="page">The page to sign on.</param>
    /// <returns>The checked signatures.</returns>
    private static async Task<List<DocumentSignature>> SignAndCheckAsync(byte[] source, X509Certificate2 certificate, int page)
    {
        var signed = PdfSigner.Sign(source, certificate, new(page, Reason, "Brisbane", TimeProvider.System.GetUtcNow()));
        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-signed-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, signed);
        try
        {
            using var document = new PdfiumEngine().Open(path, null);
            var raw = ((ISignatureSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(ISignatureSource))!).GetSignatures();
            var results = new List<DocumentSignature>();
            foreach (var signature in raw)
            {
                results.Add(SignatureVerifier.Verify(signature, path, [certificate]));
            }

            return results;
        }
        finally
        {
            File.Delete(path);
        }
    }
}
