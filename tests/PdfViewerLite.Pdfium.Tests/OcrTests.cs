// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Ocr;
using PdfViewerLite.Ocr;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests for giving scanned pages a text layer with Tesseract and PDFium.</summary>
public sealed class OcrTests
{
    /// <summary>The scan resolution in pixels per point (300 DPI).</summary>
    private const float ScanScale = 300F / 72F;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>How far, in points, a recognised word may sit from where it was printed.</summary>
    private const float PositionTolerance = 6;

    /// <summary>The top of the body sentence on the test page, in page space.</summary>
    private const float SentenceTop = 106;

    /// <summary>The fewest words expected on the test page.</summary>
    private const int MinimumWords = 10;

    /// <summary>The English language data file.</summary>
    private const string EnglishData = "eng.traineddata";

    /// <summary>The luma of white.</summary>
    private const byte WhiteLuma = 255;

    /// <summary>The luma of pure red.</summary>
    private const byte RedLuma = 76;

    /// <summary>The luma of pure blue.</summary>
    private const byte BlueLuma = 28;

    /// <summary>The scanned test page, made once.</summary>
    private static readonly Lazy<byte[]> Scan = new(CreateScan);

    /// <summary>Verifies a scanned page becomes searchable, with the words where they appear, and survives saving.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecognisesScannedPage()
    {
        using var engine = RequireEngine();
        using var scan = new TempDocument(Scan.Value);
        var before = scan.Document.GetCharacterCount(0);
        var result = OcrRunner.RecognizePage(scan.Document, (ITextLayerWriter)scan.Document, engine, 0, []);
        var matches = new List<TextMatch>();
        scan.Document.Find(0, "quick", default, matches);
        var bounds = new List<PageRect>();
        if (matches.Count > 0)
        {
            scan.Document.GetTextBounds(0, matches[0].Start, matches[0].Length, bounds);
        }

        var unsaved = ((IAnnotationEditor)scan.Document).HasUnsavedChanges;
        var saved = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-ocr-{Guid.NewGuid():N}.pdf");
        try
        {
            await using (var stream = File.Create(saved))
            {
                _ = ((IAnnotationEditor)scan.Document).Save(stream);
            }

            using var reopened = new PdfiumEngine().Open(saved, null);
            var reopenedMatches = new List<TextMatch>();
            reopened.Find(0, "lazy", default, reopenedMatches);

            await Assert.That(before).IsEqualTo(0);
            await Assert.That(result.Status).IsEqualTo(OcrPageStatus.Recognized);
            await Assert.That(result.Words).IsGreaterThan(MinimumWords);
            await Assert.That(unsaved).IsTrue();
            await Assert.That(matches.Count).IsEqualTo(1);
            await Assert.That(bounds.Count).IsGreaterThan(0);
            await Assert.That(Math.Abs(bounds[0].Top - SentenceTop)).IsLessThan(PositionTolerance);
            await Assert.That(reopenedMatches.Count).IsEqualTo(1);
        }
        finally
        {
            File.Delete(saved);
        }
    }

    /// <summary>Verifies pages that already have text are left alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkipsPagesWithText()
    {
        using var engine = RequireEngine();
        using var text = new TempDocument(TestPdf.Create(1));
        var count = text.Document.GetCharacterCount(0);
        var result = OcrRunner.RecognizePage(text.Document, (ITextLayerWriter)text.Document, engine, 0, []);

        await Assert.That(result.Status).IsEqualTo(OcrPageStatus.AlreadyHasText);
        await Assert.That(text.Document.GetCharacterCount(0)).IsEqualTo(count);
        await Assert.That(((IAnnotationEditor)text.Document).HasUnsavedChanges).IsFalse();
    }

    /// <summary>Verifies a missing language reports the engine as unavailable instead of failing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingLanguageIsUnavailable()
    {
        using var engine = new TesseractEngine("no-such-language");
        using var scan = new TempDocument(Scan.Value);
        var result = OcrRunner.RecognizePage(scan.Document, (ITextLayerWriter)scan.Document, engine, 0, []);

        await Assert.That(engine.IsAvailable).IsFalse();
        await Assert.That(result.Status).IsEqualTo(OcrPageStatus.Unavailable);
        await Assert.That(engine.Status).IsEqualTo(TesseractEngine.IsLibraryAvailable() ? OcrEngineStatus.LanguageMissing : OcrEngineStatus.LibraryMissing);
    }

    /// <summary>Verifies the Tesseract and English data shipped with the app are used ahead of any system copy.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UsesTheShippedLibraryAndEnglish()
    {
        using var engine = RequireEngine();
        var bundled = TesseractEngine.BundledLibraryPath;

        await Assert.That(bundled).IsNotNull();
        await Assert.That(TesseractEngine.LibraryPath).IsEqualTo(bundled);
        await Assert.That(engine.DataDirectory).IsEqualTo(TesseractEngine.BundledDataDirectory);
        await Assert.That(File.Exists(Path.Combine(engine.DataDirectory!, EnglishData))).IsTrue();
    }

    /// <summary>Verifies a downloaded pack in the pack folder is used ahead of the system's language data.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UsesDownloadedPacks()
    {
        using var bundled = RequireEngine();
        var installed = TesseractEngine.FindDataDirectory("eng")!;
        var folder = Path.Combine(Path.GetTempPath(), $"tessdata-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        try
        {
            File.Copy(Path.Combine(installed, EnglishData), Path.Combine(folder, EnglishData));
            using var engine = new TesseractEngine("eng", folder);
            using var scan = new TempDocument(Scan.Value);
            var result = OcrRunner.RecognizePage(scan.Document, (ITextLayerWriter)scan.Document, engine, 0, []);

            await Assert.That(TesseractEngine.FindDataDirectory("eng", folder)).IsEqualTo(folder);
            await Assert.That(engine.Status).IsEqualTo(OcrEngineStatus.Ready);
            await Assert.That(result.Status).IsEqualTo(OcrPageStatus.Recognized);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Verifies greyscale conversion uses luma weights.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConvertsToGrey()
    {
        byte[] bgra = [0, 0, 0, 255, 255, 255, 255, 255, 0, 0, 255, 255, 255, 0, 0, 255];
        var grey = new byte[4];
        OcrRunner.ToGrey(bgra, grey);

        await Assert.That(grey[0]).IsEqualTo((byte)0);
        await Assert.That(grey[1]).IsEqualTo(WhiteLuma);
        await Assert.That(grey[2]).IsEqualTo(RedLuma);
        await Assert.That(grey[3]).IsEqualTo(BlueLuma);
    }

    /// <summary>Creates an English engine, skipping the test only on runtimes the app ships no Tesseract for.</summary>
    /// <returns>The engine.</returns>
    /// <exception cref="InvalidOperationException">The shipped Tesseract did not start.</exception>
    /// <exception cref="TUnit.Core.Exceptions.SkipTestException">No Tesseract ships for this runtime and none is installed.</exception>
    private static TesseractEngine RequireEngine()
    {
        var engine = new TesseractEngine("eng");
        if (engine.IsAvailable)
        {
            return engine;
        }

        engine.Dispose();

        // Every runtime the app ships for carries Tesseract and English, so there a failure to start is a fault.
        if (TesseractEngine.BundledLibraryPath is { } bundled)
        {
            throw new InvalidOperationException($"The shipped Tesseract at {bundled} did not start: {engine.Status}.");
        }

        throw new TUnit.Core.Exceptions.SkipTestException("No Tesseract ships for this runtime and none is installed.");
    }

    /// <summary>Renders the first test page at scanning resolution and wraps it in an image-only PDF.</summary>
    /// <returns>The scan.</returns>
    private static byte[] CreateScan()
    {
        using var source = new TempDocument(TestPdf.Create(1));
        var size = source.Document.GetPageSizes()[0];
        var width = (int)MathF.Ceiling(size.Width * ScanScale);
        var height = (int)MathF.Ceiling(size.Height * ScanScale);
        var pixels = new byte[width * height * BytesPerPixel];
        _ = source.Document.Render(new(0, ScanScale, PageRotation.None, 0, 0, RenderFlags.None), new(pixels, width, height, width * BytesPerPixel));
        var grey = new byte[width * height];
        OcrRunner.ToGrey(pixels, grey);
        return TestPdf.CreateScan(grey, width, height);
    }

    /// <summary>A document opened from bytes written to a temporary file.</summary>
    private sealed class TempDocument : IDisposable
    {
        /// <summary>The file.</summary>
        private readonly string _path;

        /// <summary>Initializes a new instance of the <see cref="TempDocument"/> class.</summary>
        /// <param name="bytes">The PDF.</param>
        public TempDocument(byte[] bytes)
        {
            _path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-ocrsrc-{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(_path, bytes);
            Document = new PdfiumEngine().Open(_path, null);
        }

        /// <summary>Gets the document.</summary>
        public IDocument Document { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Document.Dispose();
            File.Delete(_path);
        }
    }
}
