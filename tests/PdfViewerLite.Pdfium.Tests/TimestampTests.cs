// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using PdfViewerLite.Core.Signatures;
using PdfViewerLite.Core.Signatures.Signing;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>
/// Tests for trusted timestamps: a timestamp on a signature is checked and sets the time the certificate is checked at,
/// so a signature stays valid after its certificate expires; a document timestamp is checked over the whole file.
/// </summary>
public sealed class TimestampTests
{
    /// <summary>The pages in the generated document.</summary>
    private const int Pages = 2;

    /// <summary>How long ago the expired certificate signed: longer than its one-year life.</summary>
    private const int YearsAgo = 3;

    /// <summary>A signature followed by a document timestamp.</summary>
    private const int SignatureAndTimestamp = 2;

    /// <summary>The reason recorded.</summary>
    private const string Reason = "Approved";

    /// <summary>A timestamped signature reports its authority and time, valid and trusted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChecksASignatureTimestamp()
    {
        var now = TimeProvider.System.GetUtcNow();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        using var authority = new TestTimestampAuthority(now);
        var signed = await PdfSigner.SignAsync(TestPdf.Create(Pages), certificate, new(0, Reason, string.Empty, now), authority, CancellationToken.None);

        var result = await CheckAsync(signed, [certificate, authority.Certificate]);

        await Assert.That(result[0].Integrity).IsEqualTo(SignatureIntegrity.Intact);
        await Assert.That(result[0].IsTrusted).IsTrue();
        await Assert.That(result[0].Timestamp).IsNotNull();
        await Assert.That(result[0].Timestamp!.IsValid).IsTrue();
        await Assert.That(result[0].Timestamp!.IsTrusted).IsTrue();
        await Assert.That(result[0].Timestamp!.Authority).IsEqualTo(TestTimestampAuthority.Name);
        await Assert.That(result[0].CheckedAt).IsNotNull();
        await Assert.That(result[0].TimestampSummary).Contains(TestTimestampAuthority.Name);
    }

    /// <summary>A certificate that has since expired is still trusted when a valid timestamp shows it signed in time.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TrustsAnExpiredCertificateThatSignedInTime()
    {
        var past = TimeProvider.System.GetUtcNow().AddYears(-YearsAgo);
        using var certificate = TestSignedPdf.CreateCertificate(new FixedClock(past));
        using var authority = new TestTimestampAuthority(past.AddDays(1));
        var stamped = await PdfSigner.SignAsync(TestPdf.Create(Pages), certificate, new(0, Reason, string.Empty, past.AddDays(1)), authority, CancellationToken.None);
        var unstamped = PdfSigner.Sign(TestPdf.Create(Pages), certificate, new(0, Reason, string.Empty, past.AddDays(1)));

        var withTimestamp = await CheckAsync(stamped, [certificate, authority.Certificate]);
        var withoutTimestamp = await CheckAsync(unstamped, [certificate]);

        await Assert.That(withTimestamp[0].IsTrusted).IsTrue();
        await Assert.That(withoutTimestamp[0].IsTrusted).IsFalse();
    }

    /// <summary>A document timestamp is checked over the whole file and reads as a timestamp, not a person's signature.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChecksADocumentTimestamp()
    {
        var now = TimeProvider.System.GetUtcNow();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        using var authority = new TestTimestampAuthority(now);
        var signed = PdfSigner.Sign(TestPdf.Create(Pages), certificate, new(0, Reason, string.Empty, now));
        var stamped = await PdfSigner.AddDocumentTimestampAsync(signed, authority, CancellationToken.None);

        var result = await CheckAsync(stamped, [certificate, authority.Certificate]);

        await Assert.That(result.Count).IsEqualTo(SignatureAndTimestamp);
        await Assert.That(result[0].Integrity).IsEqualTo(SignatureIntegrity.ChangedAfterSigning);
        await Assert.That(result[1].IsDocumentTimestamp).IsTrue();
        await Assert.That(result[1].Integrity).IsEqualTo(SignatureIntegrity.Intact);
        await Assert.That(result[1].Timestamp!.IsValid).IsTrue();
        await Assert.That(result[1].SignerName).IsEqualTo(TestTimestampAuthority.Name);
    }

    /// <summary>A file changed after its document timestamp no longer matches it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DetectsATamperedDocumentTimestamp()
    {
        var now = TimeProvider.System.GetUtcNow();
        using var authority = new TestTimestampAuthority(now);
        var stamped = await PdfSigner.AddDocumentTimestampAsync(TestPdf.Create(Pages), authority, CancellationToken.None);
        var at = stamped.AsSpan().IndexOf(") Tj"u8) - 1;
        stamped[at] = stamped[at] == (byte)'X' ? (byte)'Y' : (byte)'X';

        var result = await CheckAsync(stamped, [authority.Certificate]);

        await Assert.That(result[0].Integrity).IsEqualTo(SignatureIntegrity.Invalid);
    }

    /// <summary>Validation data stored in the file is read back and reported as long-term validation, and its certificates help the check.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsLongTermValidationData()
    {
        var now = TimeProvider.System.GetUtcNow();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        using var authority = new TestTimestampAuthority(now);
        var signed = await PdfSigner.SignAsync(TestPdf.Create(Pages), certificate, new(0, Reason, string.Empty, now), authority, CancellationToken.None);
        byte[] revocation = [0x30, 0x03, 0x0A, 0x01, 0x00];
        var withStore = PdfSigner.AddValidationData(signed, [certificate.RawData, authority.Certificate.RawData], [revocation], [revocation]);

        var store = DocumentSecurityStore.Read(withStore);
        var result = await CheckAsync(withStore, [certificate, authority.Certificate]);

        await Assert.That(store.Certificates.Count).IsEqualTo(SignatureAndTimestamp);
        await Assert.That(store.OcspResponseCount).IsEqualTo(1);
        await Assert.That(store.CrlCount).IsEqualTo(1);
        await Assert.That(result[0].HasLongTermValidation).IsTrue();
        await Assert.That(result[0].Integrity).IsEqualTo(SignatureIntegrity.ChangedAfterSigning);
        await Assert.That(result[0].TimestampSummary).Contains("Long-term validation");
        await Assert.That(DocumentSecurityStore.Read(signed).HasRevocationData).IsFalse();
    }

    /// <summary>Cancelling stops signing while it waits for the timestamp authority, and no token is issued.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellingStopsTheTimestampRequest()
    {
        var now = TimeProvider.System.GetUtcNow();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        using var authority = new TestTimestampAuthority(now);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(async () => _ = await PdfSigner.SignAsync(TestPdf.Create(Pages), certificate, new(0, Reason, string.Empty, now), authority, cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(async () => _ = await PdfSigner.AddDocumentTimestampAsync(TestPdf.Create(Pages), authority, cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(authority.Issued).IsEqualTo(0);
    }

    /// <summary>Writes a file, reads its signatures with PDFium and checks them.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="trust">Certificates to trust.</param>
    /// <returns>The checked signatures.</returns>
    private static async Task<List<DocumentSignature>> CheckAsync(byte[] bytes, X509Certificate2Collection trust)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-timestamp-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, bytes);
        try
        {
            using var document = new PdfiumEngine().Open(path, null);
            var results = new List<DocumentSignature>();
            foreach (var signature in ((ISignatureSource)document).GetSignatures())
            {
                results.Add(SignatureVerifier.Verify(signature, path, trust));
            }

            return results;
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A clock stopped at a chosen time.</summary>
    /// <param name="now">The time.</param>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => now;
    }
}
