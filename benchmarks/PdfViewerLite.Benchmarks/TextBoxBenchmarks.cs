// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Text;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures writing formatted text boxes into the PDF: in a built-in font, in an embedded installed font, reading one
/// back to edit it, and saving a page that has one. Allocations are checked from the EventPipe trace.
/// </summary>
public class TextBoxBenchmarks
{
    /// <summary>The page count of the generated document.</summary>
    private const int DocumentPages = 2;

    /// <summary>The text size in points.</summary>
    private const float Size = 12;

    /// <summary>The wrap width in points.</summary>
    private const float WrapWidth = 200;

    /// <summary>The initial capacity of the save buffer, larger than the saved file.</summary>
    private const int SaveCapacity = 1 << 20;

    /// <summary>The text written.</summary>
    private const string Text = "Ada Lovelace\n10 Analytical Way";

    /// <summary>Where text is written.</summary>
    private static readonly PagePoint TextAt = new(72, 500);

    /// <summary>The save buffer, reused.</summary>
    private readonly MemoryStream _saved = new(SaveCapacity);

    /// <summary>Text in a built-in font.</summary>
    private readonly TextFormat _standard = TextFormat.Default with { FontFamily = StandardFontFamilies.Sans, FontSize = Size, IsBold = true };

    /// <summary>Text in an installed font, embedded.</summary>
    private readonly TextFormat _embedded = TextFormat.Default with { FontFamily = TestFont.Family, FontSize = Size };

    /// <summary>The temporary file.</summary>
    private string _path = string.Empty;

    /// <summary>The document.</summary>
    private PdfiumDocument _document = null!;

    /// <summary>The text box that is read and saved.</summary>
    private int _existing;

    /// <summary>Opens the document and writes the text box that is read.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _path = TestPdf.WriteTempFile(DocumentPages);
        _document = (PdfiumDocument)new PdfiumEngine().Open(_path, null);
        _document.FontCatalog = TestFont.Catalog;
        _existing = _document.AddTextBox(1, TextAt, WrapWidth, Text, _embedded);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        _saved.Dispose();
        File.Delete(_path);
    }

    /// <summary>Writes two lines in bold Helvetica, then removes them.</summary>
    /// <returns>Whether it was removed.</returns>
    [Benchmark]
    public bool AddStandardTextBox() => _document.Remove(0, _document.AddTextBox(0, TextAt, WrapWidth, Text, _standard));

    /// <summary>Writes two lines in an installed font, embedding its subset, then removes them.</summary>
    /// <returns>Whether it was removed.</returns>
    [Benchmark]
    public bool AddEmbeddedTextBox() => _document.Remove(0, _document.AddTextBox(0, TextAt, WrapWidth, Text, _embedded));

    /// <summary>Reads a text box's text and format back to edit it.</summary>
    /// <returns>The text length.</returns>
    [Benchmark]
    public int ReadTextBox() => _document.GetTextBox(1, _existing)?.Text.Length ?? 0;

    /// <summary>Saves the document with its text box into a reused buffer.</summary>
    /// <returns>Whether it saved.</returns>
    [Benchmark]
    public bool SaveWithTextBox()
    {
        _saved.Position = 0;
        _saved.SetLength(0);
        return _document.Save(_saved);
    }
}
