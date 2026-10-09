// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Writing;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>
/// Optimises real files from the cached corpus (scans and tagged files; the tests do nothing when the corpus is not
/// cached) and a fillable form, checking pages, text, structure and looks before and after.
/// </summary>
[NotInParallel]
public sealed class CorpusOptimizationTests
{
    /// <summary>The largest corpus file optimised, so the suite stays quick.</summary>
    private const long MaxFileLength = 2L * 1024 * 1024;

    /// <summary>The mean channel difference allowed on a page's first render, out of 255.</summary>
    private const double PageTolerance = 8;

    /// <summary>The text typed into the field.</summary>
    private const string Typed = "Bob";

    /// <summary>Every small corpus file opens after optimising with the same pages and text, its first page looks the same, and tagged files keep their elements.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptimisesCorpusFiles()
    {
        foreach (var path in CorpusFiles())
        {
            var source = await File.ReadAllBytesAsync(path);
            var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Balanced);
            using var before = PdfDocumentReader.Open(source, null);
            using var after = PdfDocumentReader.Open(result.Bytes, null);

            await Assert.That(after.PageCount).IsEqualTo(before.PageCount);
            await Assert.That(result.Report.BytesAfter).IsEqualTo(result.Bytes.LongLength);
            await Assert.That(PdfDocumentTagged.GetStructureTree(after)?.ElementCount ?? 0).IsEqualTo(PdfDocumentTagged.GetStructureTree(before)?.ElementCount ?? 0);
            await Assert.That(PdfDocumentText.GetTextPage(after, 0).Text).IsEqualTo(PdfDocumentText.GetTextPage(before, 0).Text);
            await Assert.That(OptimizerTestKit.MeanDifference(OptimizerTestKit.Render(source), OptimizerTestKit.Render(result.Bytes))).IsLessThan(PageTolerance);
        }
    }

    /// <summary>A fillable form keeps its widgets and can still be filled after optimising.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormsStillFill()
    {
        var source = TestPdf.CreateForm();
        var result = OptimizerTestKit.Optimize(source, PdfOptimizeOptions.Smaller with { Cleanup = PdfCleanupItems.All });
        using var before = PdfDocumentReader.Open(source, null);
        using var after = PdfDocumentReader.Open(result.Bytes, null);
        var expected = Widgets(before);
        var widgets = Widgets(after);
        var text = widgets.FindIndex(static widget => widget.Type == PdfFieldType.Text);

        await Assert.That(widgets.Count).IsEqualTo(expected.Count);
        await Assert.That(PdfDocumentForms.GetForm(after).SetText(0, text, Typed)).IsTrue();

        using var filled = PdfDocumentReader.Open(PdfIncrementalWriter.Save(after.Objects), null);
        await Assert.That(Widgets(filled)[text].Value).IsEqualTo(Typed);
    }

    /// <summary>Lists the corpus files small enough to optimise in a test.</summary>
    /// <returns>The paths.</returns>
    private static List<string> CorpusFiles()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "corpus");
        var files = new List<string>();
        foreach (var path in Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.pdf") : [])
        {
            if (new FileInfo(path).Length <= MaxFileLength)
            {
                files.Add(path);
            }
        }

        return files;
    }

    /// <summary>Gets the first page's widgets.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The widgets.</returns>
    private static List<PdfFormWidget> Widgets(PdfDocument document)
    {
        var widgets = new List<PdfFormWidget>();
        PdfDocumentForms.GetForm(document).GetWidgets(0, widgets);
        return widgets;
    }
}
