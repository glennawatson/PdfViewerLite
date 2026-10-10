// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Signatures;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests for reading and checking digital signatures.</summary>
public sealed class SignatureTests
{
    /// <summary>The page count of the signed document.</summary>
    private const int Pages = 2;

    /// <summary>A byte inside the signed content, past the header.</summary>
    private const int TamperOffset = 200;

    /// <summary>The marked line used to change the document after signing.</summary>
    private static readonly PageRect Line = new(72, 100, 200, 14);

    /// <summary>Verifies an untouched signed document is intact, trusted when its certificate is trusted, and names its signer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VerifiesIntactSignature()
    {
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var path = Write(TestSignedPdf.Create(Pages, certificate));
        try
        {
            var raw = Read(path);
            var trusted = SignatureVerifier.Verify(raw[0], path, [certificate]);
            var untrusted = SignatureVerifier.Verify(raw[0], path, []);

            await Assert.That(raw.Count).IsEqualTo(1);
            await Assert.That(raw[0].SubFilter).IsEqualTo("adbe.pkcs7.detached");
            await Assert.That(raw[0].Reason).IsEqualTo(TestSignedPdf.Reason);
            await Assert.That(trusted.Integrity).IsEqualTo(SignatureIntegrity.Intact);
            await Assert.That(trusted.IsTrusted).IsTrue();
            await Assert.That(trusted.SignerName).IsEqualTo(TestSignedPdf.SignerName);
            await Assert.That(untrusted.Integrity).IsEqualTo(SignatureIntegrity.Intact);
            await Assert.That(untrusted.IsTrusted).IsFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies an annotation saved after signing keeps the signature valid but reports the change.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsChangesAfterSigning()
    {
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var path = Write(TestSignedPdf.Create(Pages, certificate));
        var annotated = $"{path}.annotated.pdf";
        try
        {
            using (var document = new PdfiumEngine().Open(path, null))
            {
                var editor = (IAnnotationEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(IAnnotationEditor))!;
                _ = editor.AddMarkup(0, AnnotationKind.Highlight, [Line], AnnotationColors.Sand, string.Empty);
                await using var stream = File.Create(annotated);
                _ = editor.Save(stream);
            }

            var result = SignatureVerifier.Verify(Read(annotated)[0], annotated, [certificate]);

            await Assert.That(result.Integrity).IsEqualTo(SignatureIntegrity.ChangedAfterSigning);
        }
        finally
        {
            File.Delete(path);
            File.Delete(annotated);
        }
    }

    /// <summary>Verifies altering a signed byte makes the signature invalid.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DetectsTampering()
    {
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var bytes = TestSignedPdf.Create(Pages, certificate);
        var path = Write(bytes);
        try
        {
            var raw = Read(path);
            bytes[TamperOffset] ^= 1;
            await File.WriteAllBytesAsync(path, bytes);

            var result = SignatureVerifier.Verify(raw[0], path, [certificate]);

            await Assert.That(result.Integrity).IsEqualTo(SignatureIntegrity.Invalid);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Writes a file to a temporary path.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The path.</returns>
    private static string Write(byte[] bytes)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-signed-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>Reads a file's signatures.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The signatures.</returns>
    private static IReadOnlyList<RawSignature> Read(string path)
    {
        using var document = new PdfiumEngine().Open(path, null);
        return ((ISignatureSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(ISignatureSource))!).GetSignatures();
    }
}
