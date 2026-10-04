// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests for form filling through <see cref="IFormFiller"/>.</summary>
public sealed class FormTests
{
    /// <summary>The number of fields in the test form.</summary>
    private const int FieldCount = 3;

    /// <summary>The index of the Blue option.</summary>
    private const int Blue = 2;

    /// <summary>The name typed into the text field.</summary>
    private const string TypedName = "Glenn Watson";

    /// <summary>The options of the combo box.</summary>
    private static readonly string[] Colours = ["Red", "Green", "Blue"];

    /// <summary>Verifies the fields, their kinds, values and options are read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsFields()
    {
        using var test = new FormDocument();
        var fields = Read(test.Filler);

        await Assert.That(test.Filler.HasForm).IsTrue();
        await Assert.That(fields.Count).IsEqualTo(FieldCount);
        await Assert.That(fields[0].Kind).IsEqualTo(FormFieldKind.Text);
        await Assert.That(fields[0].Name).IsEqualTo("Name");
        await Assert.That(fields[1].Kind).IsEqualTo(FormFieldKind.CheckBox);
        await Assert.That(fields[1].IsChecked).IsFalse();
        await Assert.That(fields[2].Kind).IsEqualTo(FormFieldKind.ComboBox);
        await Assert.That(fields[2].Options).IsEquivalentTo(Colours);
        await Assert.That(fields[2].SelectedOption).IsEqualTo(0);
    }

    /// <summary>Verifies each kind can be filled, the edits mark the document unsaved, and survive saving.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FillsAndSaves()
    {
        using var test = new FormDocument();
        var filler = test.Filler;
        var fields = Read(filler);

        var typed = filler.SetText(0, fields[0].Index, TypedName);
        var ticked = filler.SetChecked(0, fields[1].Index, true);
        var chosen = filler.SelectOption(0, fields[2].Index, Blue);
        var after = Read(filler);
        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-form-{Guid.NewGuid():N}.pdf");
        try
        {
            await using (var stream = File.Create(path))
            {
                _ = ((PdfViewerLite.Core.Annotations.IAnnotationEditor)test.Document).Save(stream);
            }

            using var reopened = new PdfiumEngine().Open(path, null);
            var saved = Read((IFormFiller)reopened);

            await Assert.That(typed).IsTrue();
            await Assert.That(ticked).IsTrue();
            await Assert.That(chosen).IsTrue();
            await Assert.That(after[0].Value).IsEqualTo(TypedName);
            await Assert.That(after[1].IsChecked).IsTrue();
            await Assert.That(after[2].Value).IsEqualTo("Blue");
            await Assert.That(saved[0].Value).IsEqualTo(TypedName);
            await Assert.That(saved[1].IsChecked).IsTrue();
            await Assert.That(saved[2].Value).IsEqualTo("Blue");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies a filled text field is drawn on the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawsFilledText()
    {
        const int bytesPerPixel = 4;
        const byte half = 0x80;
        using var test = new FormDocument();
        var fields = Read(test.Filler);
        _ = test.Filler.SetText(0, fields[0].Index, "WWWWWWWWWWWW");
        var bounds = fields[0].Bounds;
        var width = TestPdf.PortraitWidth;
        var height = TestPdf.PortraitHeight;
        var pixels = new byte[width * height * bytesPerPixel];

        _ = test.Document.Render(new(0, 1, PageRotation.None, 0, 0, RenderFlags.Annotations), new(pixels, width, height, width * bytesPerPixel));

        var dark = 0;
        for (var y = (int)bounds.Top; y < (int)bounds.Bottom; y++)
        {
            for (var x = (int)bounds.Left; x < (int)bounds.Right; x++)
            {
                dark += pixels[((y * width) + x) * bytesPerPixel] < half ? 1 : 0;
            }
        }

        await Assert.That(dark).IsGreaterThan(0);
    }

    /// <summary>Reads the fields of page 1.</summary>
    /// <param name="filler">The filler.</param>
    /// <returns>The fields.</returns>
    private static List<FormField> Read(IFormFiller filler)
    {
        var fields = new List<FormField>();
        filler.GetFields(0, fields);
        return fields;
    }

    /// <summary>The generated form, opened.</summary>
    private sealed class FormDocument : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="FormDocument"/> class.</summary>
        public FormDocument()
        {
            FilePath = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-formsrc-{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(FilePath, TestPdf.CreateForm());
            Document = new PdfiumEngine().Open(FilePath, null);
        }

        /// <summary>Gets the file path.</summary>
        public string FilePath { get; }

        /// <summary>Gets the document.</summary>
        public IDocument Document { get; }

        /// <summary>Gets the form filler.</summary>
        public IFormFiller Filler => (IFormFiller)Document;

        /// <inheritdoc/>
        public void Dispose()
        {
            Document.Dispose();
            File.Delete(FilePath);
        }
    }
}
