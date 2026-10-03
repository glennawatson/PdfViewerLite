// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures working out reading order on a two-column article page: reading its characters from PDFium, ordering them,
/// and flattening the result for Read Aloud. Allocations come from the EventPipe trace.
/// </summary>
public class ReadingOrderBenchmarks
{
    /// <summary>The article's pages.</summary>
    private const int Pages = 2;

    /// <summary>The characters, reused so only the library's allocations are measured.</summary>
    private readonly List<PageCharacter> _characters = [];

    /// <summary>The repeated margins.</summary>
    private readonly HashSet<string> _repeated = [with(StringComparer.Ordinal)];

    /// <summary>The document.</summary>
    private OcrDocument _document = null!;

    /// <summary>The document as a character source.</summary>
    private ITextLayoutSource _source = null!;

    /// <summary>The first page's size.</summary>
    private PageSize _size;

    /// <summary>The first page in reading order.</summary>
    private ReadingPage _page = null!;

    /// <summary>Opens a two-page article and reads its first page's characters.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = new(TestPdf.CreateArticle(Pages));
        _source = (ITextLayoutSource)_document.Document;
        _size = _document.Document.GetPageSizes()[0];
        _source.GetCharacters(0, _characters);
        _page = ReadingOrder.Analyze(0, _size, _characters, _repeated);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Reads a page's characters and their positions from PDFium.</summary>
    /// <returns>The character count.</returns>
    [Benchmark]
    public int ReadCharacters()
    {
        _characters.Clear();
        _source.GetCharacters(0, _characters);
        return _characters.Count;
    }

    /// <summary>Works out the page's reading order.</summary>
    /// <returns>The block count.</returns>
    [Benchmark]
    public int Analyze() => ReadingOrder.Analyze(0, _size, _characters, _repeated).Blocks.Count;

    /// <summary>Joins the page's blocks into text for Read Aloud.</summary>
    /// <returns>The text length.</returns>
    [Benchmark]
    public int Flatten() => ReadingDocument.Flatten(_page, out _).Length;
}
