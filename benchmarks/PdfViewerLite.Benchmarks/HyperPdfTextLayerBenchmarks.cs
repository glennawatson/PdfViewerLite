// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Ocr;
using PdfViewerLite.HyperPdf;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures writing an invisible OCR text layer of 300 words onto one page, natively with HyperPDF and with PDFium
/// for comparison. Each iteration starts from a freshly opened blank page. Allocations are checked from the EventPipe
/// trace.
/// </summary>
[InvocationCount(1, 1)]
public class HyperPdfTextLayerBenchmarks
{
    /// <summary>The words on the page.</summary>
    private const int WordCount = 300;

    /// <summary>The words in each row.</summary>
    private const int WordsPerRow = 6;

    /// <summary>The width of a word's box.</summary>
    private const float WordWidth = 80;

    /// <summary>The height of a word's box.</summary>
    private const float WordHeight = 12;

    /// <summary>The space between words across the page.</summary>
    private const float ColumnStep = 90;

    /// <summary>The space between rows down the page.</summary>
    private const float RowStep = 15;

    /// <summary>The margin around the words.</summary>
    private const float Margin = 20;

    /// <summary>The recogniser's confidence in each word.</summary>
    private const float Confidence = 90;

    /// <summary>The page width in points.</summary>
    private const int PageWidth = 612;

    /// <summary>The page height in points.</summary>
    private const int PageHeight = 792;

    /// <summary>The words.</summary>
    private readonly OcrWord[] _words = new OcrWord[WordCount];

    /// <summary>The blank page's file.</summary>
    private string _path = string.Empty;

    /// <summary>The document opened with HyperPDF for this iteration.</summary>
    private HyperPdfDocument? _native;

    /// <summary>The native text layer writer.</summary>
    private HyperPdfAnnotations _nativeWriter = null!;

    /// <summary>The document opened with PDFium for this iteration.</summary>
    private PdfViewerLite.Core.Documents.IDocument? _pdfium;

    /// <summary>Makes the blank page and the words.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"hyperpdf-textlayer-bench-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(_path, MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << >> >>")));
        for (var i = 0; i < _words.Length; i++)
        {
            var row = Math.DivRem(i, WordsPerRow, out var column);
            _words[i] = new(string.Create(CultureInfo.InvariantCulture, $"Word{i}"), new(Margin + (column * ColumnStep), Margin + (row * RowStep), WordWidth, WordHeight), Confidence);
        }
    }

    /// <summary>Deletes the page.</summary>
    [GlobalCleanup]
    public void Cleanup() => File.Delete(_path);

    /// <summary>Opens fresh copies of the page.</summary>
    [IterationSetup]
    public void OpenPage()
    {
        var native = (HyperPdfDocument)new HyperPdfEngine().Open(_path, null);
        _native = native;
        _nativeWriter = native.Annotations;
        _pdfium = new PdfiumEngine().Open(_path, null);
    }

    /// <summary>Closes the pages.</summary>
    [IterationCleanup]
    public void ClosePage()
    {
        _native?.Dispose();
        _pdfium?.Dispose();
    }

    /// <summary>Writes the words natively.</summary>
    /// <returns>The number written.</returns>
    [Benchmark]
    public int WriteNative() => _nativeWriter.AddTextLayer(0, _words);

    /// <summary>Writes the words with PDFium.</summary>
    /// <returns>The number written.</returns>
    [Benchmark(Baseline = true)]
    public int WritePdfium() => ((ITextLayerWriter)_pdfium!).AddTextLayer(0, _words);
}
