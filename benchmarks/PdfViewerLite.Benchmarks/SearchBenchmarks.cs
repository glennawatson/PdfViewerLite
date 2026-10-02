// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures text search and extraction on a page.</summary>
[MemoryDiagnoser]
public class SearchBenchmarks
{
    /// <summary>The page count of the generated document.</summary>
    private const int DocumentPages = 10;

    /// <summary>Reused match list.</summary>
    private readonly List<TextMatch> _matches = [];

    /// <summary>The temporary file.</summary>
    private string _path = string.Empty;

    /// <summary>The document.</summary>
    private IDocument _document = null!;

    /// <summary>Opens the document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _path = TestPdf.WriteTempFile(DocumentPages);
        _document = new PdfiumEngine().Open(_path, null);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        File.Delete(_path);
    }

    /// <summary>Finds a phrase on a page.</summary>
    /// <returns>The match count.</returns>
    [Benchmark]
    public int FindOnPage()
    {
        _matches.Clear();
        _document.Find(1, "lazy dog", SearchOptions.None, _matches);
        return _matches.Count;
    }

    /// <summary>Extracts a page's text.</summary>
    /// <returns>The text length.</returns>
    [Benchmark]
    public int ExtractPageText() => _document.GetText(1, 0, _document.GetCharacterCount(1)).Length;
}
