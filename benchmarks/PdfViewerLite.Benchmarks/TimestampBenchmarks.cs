// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Signatures;
using PdfViewerLite.Core.Signatures.Signing;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures trusted timestamps and long-term validation: signing with a timestamp from a local authority, adding a
/// document timestamp, reading the document security store, and checking a timestamped signature against it.
/// Allocations are checked from the EventPipe trace.
/// </summary>
public class TimestampBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int Pages = 4;

    /// <summary>The signing certificate.</summary>
    private X509Certificate2 _certificate = null!;

    /// <summary>The timestamp authority.</summary>
    private TestTimestampAuthority _authority = null!;

    /// <summary>The unsigned document.</summary>
    private byte[] _document = [];

    /// <summary>The signed, timestamped document with a security store.</summary>
    private byte[] _validated = [];

    /// <summary>The signed document's file.</summary>
    private string _path = string.Empty;

    /// <summary>The signature as stored.</summary>
    private RawSignature _signature = null!;

    /// <summary>The certificates trusted for the check.</summary>
    private X509Certificate2Collection _trust = null!;

    /// <summary>The security store, read once.</summary>
    private DocumentSecurityStore _store = null!;

    /// <summary>The signing request.</summary>
    private SigningRequest _request;

    /// <summary>Creates the certificate, authority and documents.</summary>
    /// <returns>A task.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        var now = TimeProvider.System.GetUtcNow();
        _certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        _authority = new(now);
        _trust = [_certificate, _authority.Certificate];
        _request = new(0, "Approved", "Brisbane", now);
        _document = TestPdf.Create(Pages);
        var signed = await PdfSigner.SignAsync(_document, _certificate, _request, _authority, CancellationToken.None).ConfigureAwait(false);
        _validated = PdfSigner.AddValidationData(signed, [_certificate.RawData, _authority.Certificate.RawData], [], []);
        _path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-timestamp-bench-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(_path, _validated).ConfigureAwait(false);
        using var opened = new PdfiumEngine().Open(_path, null);
        _signature = ((ISignatureSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(opened, typeof(ISignatureSource))!).GetSignatures()[0];
        _store = DocumentSecurityStore.Read(_validated);
    }

    /// <summary>Releases the certificates and deletes the file.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _certificate.Dispose();
        _authority.Dispose();
        File.Delete(_path);
    }

    /// <summary>Signs with a trusted timestamp.</summary>
    /// <returns>The signed length.</returns>
    [Benchmark]
    public async Task<int> SignWithTimestamp() => (await PdfSigner.SignAsync(_document, _certificate, _request, _authority, CancellationToken.None).ConfigureAwait(false)).Length;

    /// <summary>Adds a document timestamp.</summary>
    /// <returns>The stamped length.</returns>
    [Benchmark]
    public async Task<int> AddDocumentTimestamp() => (await PdfSigner.AddDocumentTimestampAsync(_document, _authority, CancellationToken.None).ConfigureAwait(false)).Length;

    /// <summary>Reads the document security store.</summary>
    /// <returns>The stored certificates.</returns>
    [Benchmark]
    public int ReadSecurityStore() => DocumentSecurityStore.Read(_validated).Certificates.Count;

    /// <summary>Checks a timestamped signature as of its timestamp, with the store's certificates.</summary>
    /// <returns>Whether it is trusted.</returns>
    [Benchmark]
    public bool VerifyTimestamped() => SignatureVerifier.Verify(_signature, _path, _trust, _store).IsTrusted;
}
