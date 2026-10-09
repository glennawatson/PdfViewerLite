// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Writing;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Forms.Scripting;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Reads and fills the same forms through PDFium and HyperPDF, then compares what the app sees and what the saved files hold.</summary>
public sealed class FormParityTests
{
    /// <summary>The check box of the simple form.</summary>
    private const int TickIndex = 1;

    /// <summary>The combo box of the simple form.</summary>
    private const int ComboIndex = 2;

    /// <summary>The option chosen in the combo box.</summary>
    private const int ChosenOption = 2;

    /// <summary>The comb form's text field limited to six characters.</summary>
    private const int CodeIndex = 1;

    /// <summary>The comb form's check box.</summary>
    private const int AgreeIndex = 2;

    /// <summary>The comb form's first radio button.</summary>
    private const int SmallIndex = 3;

    /// <summary>The comb form's second radio button.</summary>
    private const int LargeIndex = 4;

    /// <summary>The option chosen in the list box.</summary>
    private const int ListOption = 3;

    /// <summary>The forms compared.</summary>
    public enum Sample
    {
        /// <summary>A text field, check box and combo box.</summary>
        Form = 0,

        /// <summary>A form with comb fields and a radio group.</summary>
        CombForm = 1,

        /// <summary>A form with scripts.</summary>
        CalculatedForm = 2,

        /// <summary>A form with multi-line, password, list, rotated, inherited and read-only fields.</summary>
        RichForm = 3,
    }

    /// <summary>The kinds of edit.</summary>
    private enum FormEditKind
    {
        /// <summary>Sets text.</summary>
        Text = 0,

        /// <summary>Switches a button.</summary>
        Check = 1,

        /// <summary>Selects an option.</summary>
        Option = 2,
    }

    /// <summary>Every field reads the same through both engines.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Sample.Form)]
    [Arguments(Sample.CombForm)]
    [Arguments(Sample.CalculatedForm)]
    [Arguments(Sample.RichForm)]
    public async Task FieldsMatchPdfium(Sample sample)
    {
        using var pair = new EnginePair(Create(sample));

        await Assert.That(Describe(pair.HyperPdf)).IsEqualTo(Describe(pair.Pdfium));
        await Assert.That(((IFormFiller)pair.HyperPdf).HasForm).IsEqualTo(((IFormFiller)pair.Pdfium).HasForm);
    }

    /// <summary>The scripts recognised on each field match.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Sample.CalculatedForm)]
    [Arguments(Sample.Form)]
    [Arguments(Sample.RichForm)]
    public async Task ScriptsMatchPdfium(Sample sample)
    {
        using var pair = new EnginePair(Create(sample));
        var expected = new List<FieldScripts>();
        var actual = new List<FieldScripts>();
        ((IFormScriptSource)pair.Pdfium).GetScripts(0, expected);
        ((IFormScriptSource)pair.HyperPdf).GetScripts(0, actual);

        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(actual[i].Index).IsEqualTo(expected[i].Index);
            await Assert.That(actual[i].Name).IsEqualTo(expected[i].Name);
            await Assert.That(actual[i].Keystroke.Function).IsEqualTo(expected[i].Keystroke.Function);
            await Assert.That(actual[i].Format.Arguments).IsEquivalentTo(expected[i].Format.Arguments);
            await Assert.That(actual[i].Validate.Function).IsEqualTo(expected[i].Validate.Function);
            await Assert.That(actual[i].Calculate.Fields).IsEquivalentTo(expected[i].Calculate.Fields);
            await Assert.That(actual[i].Calculate.Expression).IsEqualTo(expected[i].Calculate.Expression);
        }
    }

    /// <summary>The same edits give the same results, fields and saved files.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Sample.Form)]
    [Arguments(Sample.CombForm)]
    [Arguments(Sample.RichForm)]
    public async Task EditsMatchPdfium(Sample sample)
    {
        using var pair = new EnginePair(Create(sample));
        var differences = new List<string>();
        foreach (var edit in GetEdits(sample))
        {
            var expected = Apply((IFormFiller)pair.Pdfium, edit);
            var actual = Apply((IFormFiller)pair.HyperPdf, edit);
            if (expected != actual)
            {
                differences.Add(string.Create(CultureInfo.InvariantCulture, $"{edit}: pdfium={expected} hyperpdf={actual}"));
            }
        }

        var savedByPdfium = SaveThroughPdfium(pair.Pdfium);
        var savedByHyperPdf = SaveManaged(pair.HyperPdf);
        try
        {
            using var fromPdfium = new PdfiumEngine().Open(savedByPdfium, null);
            using var fromHyperPdf = new PdfiumEngine().Open(savedByHyperPdf, null);
            using var reopenedNative = new HyperPdfEngine().Open(savedByHyperPdf, null);

            await Assert.That(Describe(pair.HyperPdf)).IsEqualTo(Describe(pair.Pdfium));
            await Assert.That(Describe(fromHyperPdf)).IsEqualTo(Describe(fromPdfium));
            await Assert.That(Describe(reopenedNative)).IsEqualTo(Describe(fromPdfium));
            await Assert.That(differences).IsEmpty();
        }
        finally
        {
            File.Delete(savedByPdfium);
            File.Delete(savedByHyperPdf);
        }
    }

    /// <summary>Creates a sample.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Create(Sample sample) => sample switch
    {
        Sample.Form => TestPdf.CreateForm(),
        Sample.CombForm => TestPdf.CreateCombForm(),
        Sample.CalculatedForm => TestPdf.CreateCalculatedForm(),
        _ => FormSamples.CreateRichForm(),
    };

    /// <summary>Lists the edits to make to a sample.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>The edits, in order.</returns>
    private static List<FormEdit> GetEdits(Sample sample) => sample switch
    {
        Sample.Form =>
        [
            new(FormEditKind.Text, 0, "Glenn Watson", 0),
            new(FormEditKind.Check, TickIndex, string.Empty, 1),
            new(FormEditKind.Option, ComboIndex, string.Empty, ChosenOption),
            new(FormEditKind.Check, TickIndex, string.Empty, 0),
        ],
        Sample.CombForm =>
        [
            new(FormEditKind.Text, CodeIndex, "AB1234", 0),
            new(FormEditKind.Text, CodeIndex, "TOOLONG1", 0),
            new(FormEditKind.Check, AgreeIndex, string.Empty, 1),
            new(FormEditKind.Check, SmallIndex, string.Empty, 1),
            new(FormEditKind.Check, LargeIndex, string.Empty, 1),
            new(FormEditKind.Check, LargeIndex, string.Empty, 0),
        ],
        _ =>
        [
            new(FormEditKind.Text, FormSamples.NotesIndex, "One two three four five six seven eight nine ten eleven twelve", 0),
            new(FormEditKind.Text, FormSamples.SecretIndex, "pw", 0),
            new(FormEditKind.Text, FormSamples.NotesIndex, "first\nsecond\r\nthird", 0),
            new(FormEditKind.Text, FormSamples.LimitedIndex, "a\nb", 0),
            new(FormEditKind.Text, FormSamples.StreetIndex, "café 中", 0),
            new(FormEditKind.Text, FormSamples.NotesIndex, string.Empty, 0),
            new(FormEditKind.Option, FormSamples.PickIndex, string.Empty, ListOption),
            new(FormEditKind.Option, FormSamples.ColourIndex, string.Empty, 0),
            new(FormEditKind.Text, FormSamples.LimitedIndex, "123456789", 0),
            new(FormEditKind.Check, FormSamples.PlanFirstIndex, string.Empty, 1),
            new(FormEditKind.Check, FormSamples.PlanSecondIndex, string.Empty, 1),
            new(FormEditKind.Check, FormSamples.AgreeIndex, string.Empty, 1),
            new(FormEditKind.Text, FormSamples.StreetIndex, "Elm Street", 0),
            new(FormEditKind.Text, FormSamples.ZipIndex, "98765", 0),
            new(FormEditKind.Text, FormSamples.SidewaysIndex, "Hello", 0),
        ],
    };

    /// <summary>Makes one edit.</summary>
    /// <param name="filler">The form.</param>
    /// <param name="edit">The edit.</param>
    /// <returns>Whether it succeeded.</returns>
    private static bool Apply(IFormFiller filler, FormEdit edit) => edit.Kind switch
    {
        FormEditKind.Text => filler.SetText(0, edit.Index, edit.Text),
        FormEditKind.Check => filler.SetChecked(0, edit.Index, edit.Number != 0),
        _ => filler.SelectOption(0, edit.Index, edit.Number),
    };

    /// <summary>Describes every field of page 1, one line each, so two documents can be compared as text.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The description.</returns>
    private static string Describe(IDocument document)
    {
        var fields = new List<FormField>();
        ((IFormFiller)document).GetFields(0, fields);
        var text = new StringBuilder();
        foreach (var field in fields)
        {
            var bounds = field.Bounds;
            _ = text.Append(CultureInfo.InvariantCulture, $"{field.Index} {field.Name} {field.Kind} ");
            _ = text.Append(CultureInfo.InvariantCulture, $"[{bounds.Left:0.##} {bounds.Top:0.##} {bounds.Width:0.##} {bounds.Height:0.##}] ");
            _ = text.Append(CultureInfo.InvariantCulture, $"value={field.Value} checked={field.IsChecked} ");
            _ = text.Append(CultureInfo.InvariantCulture, $"options={string.Join('|', field.Options)} selected={field.SelectedOption} ");
            _ = text.Append(CultureInfo.InvariantCulture, $"ro={field.IsReadOnly} req={field.IsRequired} multi={field.IsMultiline} ");

            // PDFium reads /MaxLen only from the widget itself, so a limit written on a parent field is not compared.
            var limit = field.Name == "Zip" ? "inherited" : $"max={field.MaxLength} comb={field.IsComb}";
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"{limit} size={field.FontSize:0.##}");
        }

        return text.ToString();
    }

    /// <summary>Saves a PDFium document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The file path.</returns>
    private static string SaveThroughPdfium(IDocument document)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-formparity-pdfium-{Guid.NewGuid():N}.pdf");
        using var stream = File.Create(path);
        _ = ((IAnnotationEditor)document).Save(stream);
        return path;
    }

    /// <summary>Saves the managed edits of a HyperPDF document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The file path.</returns>
    private static string SaveManaged(IDocument document)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-formparity-managed-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, PdfIncrementalWriter.Save(((HyperPdfDocument)document).Document.Objects));
        return path;
    }

    /// <summary>One edit of a form.</summary>
    /// <param name="Kind">The kind of edit.</param>
    /// <param name="Index">The widget index.</param>
    /// <param name="Text">The text, for a text edit.</param>
    /// <param name="Number">The option, or 1 to switch a button on.</param>
    private sealed record FormEdit(FormEditKind Kind, int Index, string Text, int Number);
}
