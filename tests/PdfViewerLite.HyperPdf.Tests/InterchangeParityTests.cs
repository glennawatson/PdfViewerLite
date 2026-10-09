// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Interchange;
using HyperPdfLibrary.Writing;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// PDFium has no public FDF or XFDF import, so the form values and annotations that HyperPDF imports are saved and read
/// back through PDFium, and compared with what PDFium holds after the same edits made through its form filler.
/// </summary>
public sealed class InterchangeParityTests
{
    /// <summary>The option of the colour combo box named by its export value "g".</summary>
    private const int GreenOption = 1;

    /// <summary>The annotations in the XFDF sample.</summary>
    private const int ImportedAnnotations = 2;

    /// <summary>The page the highlight is on.</summary>
    private const int HighlightPage = 0;

    /// <summary>Gets an XFDF file with a highlight and a text note.</summary>
    private static ReadOnlySpan<byte> Annotations => """
        <xfdf xmlns="http://ns.adobe.com/xfdf/"><annots>
          <highlight page="0" rect="72,690,300,700" color="#FFFF00" title="Ann" name="h1" coords="72,700,300,700,72,690,300,690">
            <contents>Check this</contents>
          </highlight>
          <text page="0" rect="100,100,120,120" title="Bea" icon="Comment" name="t1"><contents>A note</contents></text>
        </annots></xfdf>
        """u8;

    /// <summary>Form values imported from FDF read back through PDFium as the same edits made through PDFium's form filler.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImportedFormValuesMatchPdfiumEdits()
    {
        using var pair = new EnginePair(FormSamples.CreateRichForm());
        var filler = (IFormFiller)pair.Pdfium;
        _ = filler.SetText(0, FormSamples.NotesIndex, "Hello");
        _ = filler.SetText(0, FormSamples.SecretIndex, "pw2");
        _ = filler.SelectOption(0, FormSamples.ColourIndex, GreenOption);
        _ = filler.SetText(0, FormSamples.StreetIndex, "Elm Street");
        _ = filler.SetText(0, FormSamples.ZipIndex, "98765");
        _ = filler.SetChecked(0, FormSamples.AgreeIndex, true);

        var data = new PdfInterchangeData();
        data.Fields.Add(new("Notes", ["Hello"]));
        data.Fields.Add(new("Secret", ["pw2"]));
        data.Fields.Add(new("Colour", ["g"]));
        data.Fields.Add(new("Address.Street", ["Elm Street"]));
        data.Fields.Add(new("Zip", ["98765"]));
        data.Fields.Add(new("Agree", ["Yes"]) { ValueIsName = true });
        var result = PdfInterchange.ImportFdf(((HyperPdfDocument)pair.HyperPdf).Document, FdfWriter.Write(data));

        var fromPdfium = SaveThroughPdfium(pair.Pdfium);
        var fromHyperPdf = SaveManaged(pair.HyperPdf);
        try
        {
            using var expected = new PdfiumEngine().Open(fromPdfium, null);
            using var actual = new PdfiumEngine().Open(fromHyperPdf, null);
            using var reopened = new HyperPdfEngine().Open(fromHyperPdf, null);

            await Assert.That(result.FieldsApplied).IsEqualTo(data.Fields.Count);
            await Assert.That(Describe(actual)).IsEqualTo(Describe(expected));
            await Assert.That(Describe(reopened)).IsEqualTo(Describe(expected));
        }
        finally
        {
            File.Delete(fromPdfium);
            File.Delete(fromHyperPdf);
        }
    }

    /// <summary>Annotations imported from XFDF are read by PDFium from the saved file with their kind, author and text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImportedAnnotationsAreReadByPdfium()
    {
        using var pair = new EnginePair(TestPdf.Create(1));
        var result = PdfInterchange.ImportXfdf(((HyperPdfDocument)pair.HyperPdf).Document, Annotations.ToArray());
        var saved = SaveManaged(pair.HyperPdf);
        try
        {
            using var pdfium = new PdfiumEngine().Open(saved, null);
            using var hyperPdf = new HyperPdfEngine().Open(saved, null);
            var seenByPdfium = new List<PageAnnotation>();
            var seenByHyperPdf = new List<PageAnnotation>();
            ((IAnnotationEditor)pdfium).GetAnnotations(HighlightPage, seenByPdfium);
            ((IAnnotationEditor)hyperPdf).GetAnnotations(HighlightPage, seenByHyperPdf);

            await Assert.That(result.AnnotationsAdded).IsEqualTo(ImportedAnnotations);
            await Assert.That(seenByPdfium.Select(static annotation => annotation.Kind)).Contains(AnnotationKind.Highlight);
            await Assert.That(seenByPdfium.Select(static annotation => annotation.Kind)).Contains(AnnotationKind.Note);
            await Assert.That(seenByPdfium.Single(static annotation => annotation.Kind == AnnotationKind.Highlight).Contents).IsEqualTo("Check this");
            await Assert.That(seenByPdfium.Single(static annotation => annotation.Kind == AnnotationKind.Highlight).Author).IsEqualTo("Ann");
            await Assert.That(Summarise(seenByHyperPdf)).IsEqualTo(Summarise(seenByPdfium));
        }
        finally
        {
            File.Delete(saved);
        }
    }

    /// <summary>Lists the kind, author and text of annotations, one per line.</summary>
    /// <param name="annotations">The annotations.</param>
    /// <returns>The summary.</returns>
    private static string Summarise(List<PageAnnotation> annotations)
    {
        var text = new StringBuilder();
        foreach (var annotation in annotations.OrderBy(static annotation => annotation.Kind))
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{annotation.Kind} {annotation.Author} {annotation.Contents}").Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Describes every field of the first page: name, kind, value and state.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The description, a line per field.</returns>
    private static string Describe(IDocument document)
    {
        var fields = new List<FormField>();
        ((IFormFiller)document).GetFields(0, fields);
        var text = new StringBuilder();
        foreach (var field in fields)
        {
            _ = text.Append(CultureInfo.InvariantCulture, $"{field.Index} {field.Name} {field.Kind} value={field.Value} checked={field.IsChecked} selected={field.SelectedOption}\n");
        }

        return text.ToString();
    }

    /// <summary>Saves a PDFium document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The file path.</returns>
    private static string SaveThroughPdfium(IDocument document)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-interchange-pdfium-{Guid.NewGuid():N}.pdf");
        using var stream = File.Create(path);
        _ = ((IAnnotationEditor)document).Save(stream);
        return path;
    }

    /// <summary>Saves the managed edits of a HyperPDF document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The file path.</returns>
    private static string SaveManaged(IDocument document)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-interchange-managed-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, PdfIncrementalWriter.Save(((HyperPdfDocument)document).Document.Objects));
        return path;
    }
}
