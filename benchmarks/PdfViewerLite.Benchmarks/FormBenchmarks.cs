// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Rendering;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures filling a form: typing into a text field, ticking a box, choosing an option, reading the fields and
/// drawing a page with its fields. Each edit alternates between two values so PDFium always has work to do.
/// Allocations are checked from the EventPipe trace.
/// </summary>
public class FormBenchmarks
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The render scale (200% at 96 DPI).</summary>
    private const float Scale = 2F * 96F / 72F;

    /// <summary>The first text value.</summary>
    private const string FirstName = "Ada Lovelace";

    /// <summary>The second text value.</summary>
    private const string SecondName = "Grace Hopper";

    /// <summary>The fields read, reused.</summary>
    private readonly List<FormField> _fields = [];

    /// <summary>The tile pixels.</summary>
    private readonly byte[] _pixels = new byte[TileGrid.TileSize * TileGrid.TileSize * BytesPerPixel];

    /// <summary>The temporary file.</summary>
    private string _path = string.Empty;

    /// <summary>The document.</summary>
    private PdfiumDocument _document = null!;

    /// <summary>The text field's widget index.</summary>
    private int _text;

    /// <summary>The check box's widget index.</summary>
    private int _checkBox;

    /// <summary>The combo box's widget index.</summary>
    private int _combo;

    /// <summary>Alternates the values set.</summary>
    private bool _flip;

    /// <summary>Opens the form.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-formbench-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(_path, TestPdf.CreateForm());
        _document = (PdfiumDocument)new PdfiumEngine().Open(_path, null);
        _document.GetFields(0, _fields);
        _text = _fields.Single(static f => f.Kind == FormFieldKind.Text).Index;
        _checkBox = _fields.Single(static f => f.Kind == FormFieldKind.CheckBox).Index;
        _combo = _fields.Single(static f => f.Kind == FormFieldKind.ComboBox).Index;
    }

    /// <summary>Closes the form.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        File.Delete(_path);
    }

    /// <summary>Replaces the text of the text field.</summary>
    /// <returns>Whether it changed.</returns>
    [Benchmark]
    public bool SetText()
    {
        _flip = !_flip;
        return _document.SetText(0, _text, _flip ? FirstName : SecondName);
    }

    /// <summary>Ticks or clears the check box.</summary>
    /// <returns>Whether it changed.</returns>
    [Benchmark]
    public bool ToggleCheckBox()
    {
        _flip = !_flip;
        return _document.SetChecked(0, _checkBox, _flip);
    }

    /// <summary>Chooses an option of the combo box.</summary>
    /// <returns>Whether it changed.</returns>
    [Benchmark]
    public bool SelectOption()
    {
        _flip = !_flip;
        return _document.SelectOption(0, _combo, _flip ? 1 : 0);
    }

    /// <summary>Reads the page's fields when nothing changed since the last read.</summary>
    /// <returns>The number of fields.</returns>
    [Benchmark]
    public int ReadFields()
    {
        _fields.Clear();
        _document.GetFields(0, _fields);
        return _fields.Count;
    }

    /// <summary>Renders the top-left tile of the form page, fields included.</summary>
    /// <returns>Whether it rendered.</returns>
    [Benchmark]
    public bool RenderFormTile()
    {
        var tileSize = TileGrid.TileSize;
        return _document.Render(new(0, Scale, PageRotation.None, 0, 0, RenderFlags.Annotations), new(_pixels, tileSize, tileSize, tileSize * BytesPerPixel));
    }
}
