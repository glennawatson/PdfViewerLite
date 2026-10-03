// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Forms.Scripting;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures the form scripts PdfViewerLite runs itself: reading a form's scripts through PDFium, recognising a
/// script, formatting a value and calculating a field. Allocations are checked from the EventPipe trace.
/// </summary>
public class FormScriptBenchmarks
{
    /// <summary>A number format script.</summary>
    private static readonly FormScript NumberFormat = FormScript.Parse("AFNumber_Format(2, 0, 0, 0, \"$\", true);");

    /// <summary>A simplified field notation calculation.</summary>
    private static readonly FormScript Expression = FormScript.Parse("/** BVCALC (Price - Discount) * Quantity EVCALC **/");

    /// <summary>The field values the calculation reads.</summary>
    private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal) { ["Price"] = "$12.50", ["Discount"] = "2.5", ["Quantity"] = "4" };

    /// <summary>The scripts read, reused.</summary>
    private readonly List<FieldScripts> _scripts = [];

    /// <summary>The temporary file.</summary>
    private string _path = string.Empty;

    /// <summary>The document.</summary>
    private PdfiumDocument _document = null!;

    /// <summary>Opens the calculated form.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-formscripts-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(_path, TestPdf.CreateCalculatedForm());
        _document = (PdfiumDocument)new PdfiumEngine().Open(_path, null);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        File.Delete(_path);
    }

    /// <summary>Reads a page's field scripts.</summary>
    /// <returns>The scripted fields.</returns>
    [Benchmark]
    public int ReadScripts()
    {
        _scripts.Clear();
        _document.GetScripts(0, _scripts);
        return _scripts.Count;
    }

    /// <summary>Recognises a calculation script.</summary>
    /// <returns>The fields it reads.</returns>
    [Benchmark]
    public int ParseScript() => FormScript.Parse("AFSimple_Calculate(\"SUM\", new Array (\"Price\", \"Tax\", \"Shipping\"));").Fields.Count;

    /// <summary>Formats a number as currency.</summary>
    /// <returns>The formatted value.</returns>
    [Benchmark]
    public string FormatCurrency() => FormScriptEngine.Format(NumberFormat, "1234.5");

    /// <summary>Calculates an expression over fields.</summary>
    /// <returns>The result.</returns>
    [Benchmark]
    public double CalculateExpression() => FormScriptEngine.TryCalculate(Expression, static name => Values.GetValueOrDefault(name), out var result) ? result : 0;
}
