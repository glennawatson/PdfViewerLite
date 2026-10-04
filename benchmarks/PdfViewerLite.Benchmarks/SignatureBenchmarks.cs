// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Signatures;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures signing and signature checking: placing a typed or drawn signature, reading the digital signatures, and
/// checking one. Allocations are checked from the EventPipe trace.
/// </summary>
public class SignatureBenchmarks
{
    /// <summary>The page count of the signed document.</summary>
    private const int Pages = 4;

    /// <summary>The typed signature size in points.</summary>
    private const float SignatureSize = 24;

    /// <summary>The drawn signature line width in points.</summary>
    private const float InkWidth = 1.5F;

    /// <summary>The size of the sample signature image.</summary>
    private const int ImageSize = 2;

    /// <summary>Opaque and soft ink with transparent paper.</summary>
    private static readonly byte[] SignatureImage = [0, 0, 0, 255, 0, 0, 0, 127, 0, 0, 0, 0, 255, 0, 0, 255];

    /// <summary>The image signature's size and position on the page.</summary>
    private static readonly PageRect ImageBounds = new(72, 600, 120, 40);

    /// <summary>Where signatures are placed.</summary>
    private static readonly PagePoint SignAt = new(72, 600);

    /// <summary>A signature-like stroke.</summary>
    private static readonly PagePoint[] Stroke = [new(100, 650), new(115, 640), new(130, 660), new(145, 645), new(160, 655), new(175, 648), new(190, 652)];

    /// <summary>The stroke lengths.</summary>
    private static readonly int[] StrokeLengths = [Stroke.Length];

    /// <summary>The trusted test certificate.</summary>
    private X509Certificate2 _certificate = null!;

    /// <summary>The trust store holding the test certificate.</summary>
    private X509Certificate2Collection _trust = null!;

    /// <summary>The temporary file.</summary>
    private string _path = string.Empty;

    /// <summary>The document.</summary>
    private PdfiumDocument _document = null!;

    /// <summary>The signature read once, for checking.</summary>
    private RawSignature _signature = null!;

    /// <summary>Creates and opens a signed document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        _trust = [_certificate];
        _path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-sigbench-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(_path, TestSignedPdf.Create(Pages, _certificate));
        _document = (PdfiumDocument)new PdfiumEngine().Open(_path, null);
        _signature = _document.GetSignatures()[0];
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        _certificate.Dispose();
        File.Delete(_path);
    }

    /// <summary>Places a typed signature, then removes it.</summary>
    /// <returns>Whether it was removed.</returns>
    [Benchmark]
    public bool PlaceTypedSignature() =>
        _document.Remove(1, _document.AddText(1, SignAt, "Glenn Watson", SignatureSize, AnnotationColors.Ink, AnnotationKind.Signature));

    /// <summary>Places a drawn signature, then removes it.</summary>
    /// <returns>Whether it was removed.</returns>
    [Benchmark]
    public bool PlaceDrawnSignature() =>
        _document.Remove(1, _document.AddInk(1, Stroke, StrokeLengths, AnnotationColors.Ink, InkWidth, AnnotationKind.Signature));

    /// <summary>Copies an image signature into the PDF, then removes it.</summary>
    /// <returns>Whether it was removed.</returns>
    [Benchmark]
    public bool PlaceImageSignature() =>
        _document.Remove(1, _document.AddImageSignature(1, ImageBounds, SignatureImage, ImageSize, ImageSize));

    /// <summary>Reads the document's digital signatures.</summary>
    /// <returns>The number read.</returns>
    [Benchmark]
    public int ReadSignatures() => _document.GetSignatures().Count;

    /// <summary>Checks the digital signature against the file and the trusted certificate.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public SignatureIntegrity VerifySignature() => SignatureVerifier.Verify(_signature, _path, _trust).Integrity;
}
