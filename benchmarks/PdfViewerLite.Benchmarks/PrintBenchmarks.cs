// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures writing the print copy of a filled, annotated form; allocations come from the EventPipe trace.</summary>
public class PrintBenchmarks
{
    /// <summary>Where the note sits.</summary>
    private static readonly PagePoint NoteAt = new(400, 120);

    /// <summary>The print copy, reused so only the library's allocations are measured.</summary>
    private readonly MemoryStream _copy = new();

    /// <summary>The document.</summary>
    private OcrDocument _document = null!;

    /// <summary>The document's editor.</summary>
    private IAnnotationEditor _editor = null!;

    /// <summary>Opens the form, fills its name field and adds a note.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = new(TestPdf.CreateForm());
        _editor = (IAnnotationEditor)_document.Document;
        var filler = (IFormFiller)_document.Document;
        List<FormField> fields = [];
        filler.GetFields(0, fields);
        _ = filler.SetText(0, fields[0].Index, "Glenn Watson");
        _ = _editor.AddNote(0, NoteAt, "Checked", AnnotationColors.Sand);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        _copy.Dispose();
    }

    /// <summary>Writes the copy handed to the print dialog.</summary>
    /// <returns>Whether it was written.</returns>
    [Benchmark]
    public bool WritePrintCopy()
    {
        _copy.Position = 0;
        _copy.SetLength(0);
        return _editor.Save(_copy);
    }
}
