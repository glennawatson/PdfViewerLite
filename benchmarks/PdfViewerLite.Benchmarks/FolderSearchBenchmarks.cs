// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Search;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures Search in Folder: opening, searching and closing one file.</summary>
public class FolderSearchBenchmarks
{
    /// <summary>The pages of the searched file.</summary>
    private const int Pages = 20;

    /// <summary>The most matches kept.</summary>
    private const int MaxMatches = 200;

    /// <summary>The engine.</summary>
    private readonly PdfiumEngine _engine = new();

    /// <summary>The searched file.</summary>
    private string _path = string.Empty;

    /// <summary>Writes the file.</summary>
    [GlobalSetup]
    public void Setup() => _path = TestPdf.WriteTempFile(Pages);

    /// <summary>Deletes the file.</summary>
    [GlobalCleanup]
    public void Cleanup() => File.Delete(_path);

    /// <summary>Searches every page of one file and closes it.</summary>
    /// <returns>The matches found.</returns>
    [Benchmark]
    public int SearchFile() => FolderSearch.SearchFile(_engine, _path, "brown fox", SearchOptions.None, MaxMatches, CancellationToken.None).Matches.Count;
}
