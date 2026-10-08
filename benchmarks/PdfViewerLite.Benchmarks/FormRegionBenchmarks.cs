// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms.Detection;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures finding places to write on a printed form, with the vector scan and with the scalar fallback: turning
/// the rendered page grey, finding the lines, boxes and character boxes, rendering and finding them together, and
/// finding the place under the pointer. Allocations are checked from the EventPipe trace.
/// </summary>
public class FormRegionBenchmarks
{
    /// <summary>The pixels per point the page is read at, as the finder reads it.</summary>
    private const float Scale = 2;

    /// <summary>The bytes per pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>A point inside the row of character boxes, the last place found.</summary>
    private static readonly PagePoint InComb = new(150, 330);

    /// <summary>The places found, reused.</summary>
    private readonly List<FormRegion> _regions = [];

    /// <summary>The detector.</summary>
    private readonly FormRegionDetector _detector = new();

    /// <summary>The finder.</summary>
    private readonly FlatFormFinder _finder = new();

    /// <summary>The rendered page.</summary>
    private byte[] _pixels = [];

    /// <summary>The grey page.</summary>
    private byte[] _luma = [];

    /// <summary>The places on the page.</summary>
    private FormRegion[] _found = [];

    /// <summary>The page width in pixels.</summary>
    private int _width;

    /// <summary>The page height in pixels.</summary>
    private int _height;

    /// <summary>The temporary file.</summary>
    private string _path = string.Empty;

    /// <summary>The document.</summary>
    private PdfiumDocument _document = null!;

    /// <summary>Gets or sets a value indicating whether the vector scan is used.</summary>
    [Params(true, false)]
    public bool Vectors { get; set; }

    /// <summary>Opens and renders the printed form.</summary>
    [GlobalSetup]
    public void Setup()
    {
        DarkPixels.UseVectors = Vectors;
        _path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-flatbench-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(_path, TestPdf.CreateFlatForm());
        _document = (PdfiumDocument)new PdfiumEngine().Open(_path, null);
        var size = _document.GetPageSizes()[0];
        _width = (int)MathF.Ceiling(size.Width * Scale);
        _height = (int)MathF.Ceiling(size.Height * Scale);
        _pixels = new byte[_width * _height * PixelBytes];
        _luma = new byte[_width * _height];
        _pixels.AsSpan().Fill(byte.MaxValue);
        _ = _document.Render(new(0, Scale, PageRotation.None, 0, 0, RenderFlags.None), new(_pixels, _width, _height, _width * PixelBytes));
        DarkPixels.ToLuminance(_pixels, _luma);
        _detector.Detect(_luma, (_width, _height, _width), Scale, _regions);
        _found = [.. _regions];
    }

    /// <summary>Closes the form.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        DarkPixels.UseVectors = true;
        _document.Dispose();
        File.Delete(_path);
    }

    /// <summary>Turns the rendered page grey.</summary>
    /// <returns>The first grey level.</returns>
    [Benchmark]
    public byte ToLuminance()
    {
        DarkPixels.ToLuminance(_pixels, _luma);
        return _luma[0];
    }

    /// <summary>Finds the places to write on the grey page.</summary>
    /// <returns>The number found.</returns>
    [Benchmark]
    public int Detect()
    {
        _regions.Clear();
        _detector.Detect(_luma, (_width, _height, _width), Scale, _regions);
        return _regions.Count;
    }

    /// <summary>Renders the page and finds its places, as the Text tool does once per page.</summary>
    /// <returns>The number found.</returns>
    [Benchmark]
    public int RenderAndDetect()
    {
        _regions.Clear();
        _finder.Find(_document, 0, _regions);
        return _regions.Count;
    }

    /// <summary>Finds the place under the pointer.</summary>
    /// <returns>Whether one was found.</returns>
    [Benchmark]
    public bool RegionAt() => FormRegion.At(_found, InComb) is not null;
}
