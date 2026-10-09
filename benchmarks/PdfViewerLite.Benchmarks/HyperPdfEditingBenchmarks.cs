// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Writing;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures HyperPDF page editing on a 100 page document: reordering every page in a transaction, reordering and
/// undoing, and saving the reordered document incrementally.
/// </summary>
public class HyperPdfEditingBenchmarks
{
    /// <summary>The pages in the sample document.</summary>
    private const int Pages = 100;

    /// <summary>The reversed page order.</summary>
    private readonly int[] _reverse = new int[Pages];

    /// <summary>The document reordered by the reorder benchmarks.</summary>
    private PdfDocument? _document;

    /// <summary>A document reordered once, for the save benchmark.</summary>
    private PdfDocument? _reordered;

    /// <summary>Opens the documents.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var bytes = TestPdf.Create(Pages);
        for (var i = 0; i < Pages; i++)
        {
            _reverse[i] = Pages - 1 - i;
        }

        _document = PdfDocument.Open(bytes, null);
        _reordered = PdfDocument.Open(bytes, null);
        _reordered.ReorderPages(_reverse);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document?.Dispose();
        _reordered?.Dispose();
    }

    /// <summary>Reverses every page in one transaction; each call reverses the previous order.</summary>
    /// <returns>The page count, so the work is not optimised away.</returns>
    [Benchmark(Baseline = true)]
    public int Reorder()
    {
        _document!.ReorderPages(_reverse);
        return _document.PageCount;
    }

    /// <summary>Reverses every page, then undoes it.</summary>
    /// <returns>The page count.</returns>
    [Benchmark]
    public int ReorderAndUndo()
    {
        _document!.ReorderPages(_reverse);
        _ = _document.Undo();
        return _document.PageCount;
    }

    /// <summary>Saves the reordered document as an incremental update.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public int SaveIncremental() => PdfIncrementalWriter.Save(_reordered!.Objects).Length;
}
