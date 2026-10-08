// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Printing;
using PdfViewerLite.Core.Text;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>
/// Tests formatted text boxes: writing them in an embedded subset of an installed font or a built in font, reading
/// them back, saving them as standard typewriter free text, reopening and editing them, and editing other programs'
/// free text.
/// </summary>
public sealed class TextBoxTests
{
    /// <summary>The font size.</summary>
    private const float Size = 20;

    /// <summary>A wrap width that fits a few words.</summary>
    private const float Wrap = 120;

    /// <summary>How close positions must be, in points.</summary>
    private const float Tolerance = 0.5F;

    /// <summary>The bytes in a rendered pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>A white channel.</summary>
    private const byte White = 0xFF;

    /// <summary>The comb boxes.</summary>
    private const int Cells = 5;

    /// <summary>The comb width.</summary>
    private const float CombWidth = 150;

    /// <summary>A colour.</summary>
    private const uint Blue = 0x1144AA;

    /// <summary>The foreign free text's size.</summary>
    private const float ForeignSize = 14;

    /// <summary>Red as 0xRRGGBB.</summary>
    private const uint Red = 0xFF0000;

    /// <summary>Two lines.</summary>
    private const int TwoLines = 2;

    /// <summary>The key of an embedded TrueType font program.</summary>
    private const string FontFile = "/FontFile2";

    /// <summary>A sentence with an accent, a composite glyph and a kerning pair.</summary>
    private const string Sentence = "Hello é AV world";

    /// <summary>A short greeting.</summary>
    private const string Greeting = "Hello é";

    /// <summary>Where text boxes go.</summary>
    private static readonly PagePoint At = new(100, 150);

    /// <summary>The format in the test font.</summary>
    private static readonly TextFormat Installed = new(TestFont.Family, Size, Blue) { IsBold = true, IsUnderline = true, Alignment = TextBoxAlignment.Center };

    /// <summary>A box in the test font is a free text annotation that keeps its text, format and place, and draws ink.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesAndReadsBack()
    {
        using var test = Open();
        var document = (PdfiumDocument)test.Document;
        var index = document.AddTextBox(0, At, Wrap, Sentence, Installed);
        var annotations = new List<PageAnnotation>();
        document.GetAnnotations(0, annotations);
        var content = document.GetTextBox(0, index);

        await Assert.That(index).IsGreaterThanOrEqualTo(0);
        await Assert.That(annotations[^1].Kind).IsEqualTo(AnnotationKind.TextBox);
        await Assert.That(annotations[^1].Contents).IsEqualTo(Sentence);
        await Assert.That(annotations[^1].Bounds.Left).IsEqualTo(At.X).Within(Tolerance);
        await Assert.That(annotations[^1].Bounds.Top).IsEqualTo(At.Y).Within(Tolerance);
        await Assert.That(annotations[^1].Bounds.Width).IsEqualTo(Wrap).Within(Tolerance);
        await Assert.That(content!.Text).IsEqualTo(Sentence);
        await Assert.That(content.Format).IsEqualTo(Installed);
        await Assert.That(content.WrapWidth).IsEqualTo(Wrap);
        await Assert.That(HasInk(document, content.Bounds)).IsTrue();
        await Assert.That(document.HasUnsavedChanges).IsTrue();
    }

    /// <summary>Saving writes a typewriter free text with a subset font and a text map; reopening reads it back for editing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesAsFreeTextAndReopens()
    {
        using var test = Open();
        var document = (PdfiumDocument)test.Document;
        _ = document.AddTextBox(0, At, 0, Greeting, Installed);
        var bytes = Save(document);
        var text = Encoding.Latin1.GetString(bytes);
        using var reopened = Reopen(bytes, out var path);
        try
        {
            var annotations = new List<PageAnnotation>();
            ((IAnnotationEditor)reopened).GetAnnotations(0, annotations);
            var content = ((ITextBoxEditor)reopened).GetTextBox(0, annotations[0].Index);

            await Assert.That(text).Contains("/FreeTextTypeWriter");
            await Assert.That(text).Contains(FontFile);
            await Assert.That(text).Contains("/ToUnicode");
            await Assert.That(text).Contains("/RC");
            await Assert.That(text).Contains("/Subtype /FreeText");
            await Assert.That(annotations.Count).IsEqualTo(1);
            await Assert.That(annotations[0].Kind).IsEqualTo(AnnotationKind.TextBox);
            await Assert.That(content!.Text).IsEqualTo(Greeting);
            await Assert.That(content.Format).IsEqualTo(Installed);
            await Assert.That(HasInk(reopened, content.Bounds)).IsTrue();
            await Assert.That(FlattenedText(reopened)).Contains(Greeting);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Built in families need no embedded font; text a built in font lacks moves to an installed font that has it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UsesBuiltInFontsAndFallsBack()
    {
        using var test = Open();
        var document = (PdfiumDocument)test.Document;
        _ = document.AddTextBox(0, At, 0, "Plain", new("Times", Size, 0) { IsItalic = true });
        _ = document.AddTextBox(0, new(At.X, At.Y + Wrap), 0, "Missing family", new("No Such Mono", Size, 0));
        var builtIn = Encoding.Latin1.GetString(Save(document));
        _ = document.AddTextBox(0, new(At.X, At.Y + Wrap + Wrap), 0, "中 αβ", new("Helvetica", Size, 0));
        var withFallback = Encoding.Latin1.GetString(Save(document));
        var annotations = new List<PageAnnotation>();
        document.GetAnnotations(0, annotations);

        await Assert.That(builtIn).Contains("/Times-Italic");
        await Assert.That(builtIn).Contains("/Courier");
        await Assert.That(builtIn).DoesNotContain(FontFile);
        await Assert.That(withFallback).Contains(FontFile);
        await Assert.That(HasInk(document, annotations[^1].Bounds)).IsTrue();
    }

    /// <summary>A face whose licence forbids embedding is never embedded; its text uses the closest built in family.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeverEmbedsRestrictedFonts()
    {
        using var test = Open();
        var document = (PdfiumDocument)test.Document;
        _ = document.AddTextBox(0, At, 0, "Licensed", new(TestFont.LockedFamily, Size, 0));
        var text = Encoding.Latin1.GetString(Save(document));

        await Assert.That(text).DoesNotContain(FontFile);
        await Assert.That(text).Contains("/Helvetica");
    }

    /// <summary>Wrapped text grows downwards at its wrap width; comb text keeps the comb's width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrapsAndCombs()
    {
        using var test = Open();
        var document = (PdfiumDocument)test.Document;
        var one = document.AddTextBox(0, At, 0, "short", Installed with { IsUnderline = false });
        var wrapped = document.AddTextBox(0, At, Wrap, "a much longer piece of text that wraps", Installed with { IsUnderline = false });
        var comb = document.AddTextBox(0, At, CombWidth, "AB123XYZ", Installed with { CombCells = Cells, Alignment = TextBoxAlignment.Left });
        var annotations = new List<PageAnnotation>();
        document.GetAnnotations(0, annotations);
        var oneBox = annotations.Single(a => a.Index == one).Bounds;
        var wrappedBox = annotations.Single(a => a.Index == wrapped).Bounds;
        var combBox = annotations.Single(a => a.Index == comb).Bounds;

        await Assert.That(wrappedBox.Height).IsGreaterThan(oneBox.Height * TwoLines);
        await Assert.That(wrappedBox.Width).IsEqualTo(Wrap).Within(Tolerance);
        await Assert.That(combBox.Width).IsEqualTo(CombWidth).Within(Tolerance);
        await Assert.That(document.GetTextBox(0, comb)!.Format.CombCells).IsEqualTo(Cells);
    }

    /// <summary>Free text written by another program reads back with its text, size, colour, weight and alignment.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsOtherProgramsFreeText()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pvl-freetext-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateWithFreeText());
        try
        {
            using var document = new PdfiumEngine().Open(path, null);
            var annotations = new List<PageAnnotation>();
            ((IAnnotationEditor)document).GetAnnotations(0, annotations);
            var content = ((ITextBoxEditor)document).GetTextBox(0, annotations[0].Index);

            await Assert.That(annotations[0].Kind).IsEqualTo(AnnotationKind.TextBox);
            await Assert.That(content!.Text).IsEqualTo(TestPdf.ForeignText);
            await Assert.That(content.Format.FontSize).IsEqualTo(ForeignSize);
            await Assert.That(content.Format.Color).IsEqualTo(Red);
            await Assert.That(content.Format.IsBold).IsTrue();
            await Assert.That(content.Format.Alignment).IsEqualTo(TextBoxAlignment.Center);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Blank text writes nothing; a removed text box cannot be read for editing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesBlankAndRemovedText()
    {
        using var test = Open();
        var document = (PdfiumDocument)test.Document;
        var blank = document.AddTextBox(0, At, 0, "  \n ", Installed);
        var index = document.AddTextBox(0, At, 0, "Gone", Installed);
        _ = document.SetRemoved(0, index, true);

        await Assert.That(blank).IsEqualTo(-1);
        await Assert.That(document.GetTextBox(0, index)).IsNull();
        await Assert.That(document.GetTextBox(0, int.MaxValue)).IsNull();
    }

    /// <summary>Opens a one page document whose text boxes may use the test fonts.</summary>
    /// <returns>The document.</returns>
    private static TestDocument Open()
    {
        var test = new TestDocument(1);
        ((PdfiumDocument)test.Document).FontCatalog = TestFont.Catalog;
        return test;
    }

    /// <summary>Saves a document to bytes.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The file.</returns>
    private static byte[] Save(PdfiumDocument document)
    {
        using var stream = new MemoryStream();
        _ = document.Save(stream);
        return stream.ToArray();
    }

    /// <summary>Writes saved bytes to a file and opens it.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="path">Receives the path, to delete.</param>
    /// <returns>The reopened document.</returns>
    private static IDocument Reopen(byte[] bytes, out string path)
    {
        path = Path.Combine(Path.GetTempPath(), $"pvl-textbox-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, bytes);
        var document = new PdfiumEngine().Open(path, null);
        ((PdfiumDocument)document).FontCatalog = TestFont.Catalog;
        return document;
    }

    /// <summary>Renders the first page and checks a box holds some ink.</summary>
    /// <param name="document">The document.</param>
    /// <param name="box">The box in page space.</param>
    /// <returns><see langword="true"/> when a pixel in the box is not white.</returns>
    private static bool HasInk(IDocument document, PageRect box)
    {
        TestPdf.GetPageSize(0, out var width, out var height);
        var pixels = new byte[width * height * PixelBytes];
        _ = document.Render(new(0, 1, PageRotation.None, 0, 0, RenderFlags.Annotations), new(pixels, width, height, width * PixelBytes));
        for (var y = (int)box.Top; y < (int)box.Bottom && y < height; y++)
        {
            var row = pixels.AsSpan(((y * width) + (int)box.Left) * PixelBytes, (int)box.Width * PixelBytes);
            if (row.ContainsAnyExcept(White))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Exports the first page with its annotations flattened into the page, and reads its text.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The page's text.</returns>
    private static string FlattenedText(IDocument document)
    {
        using var stream = new MemoryStream();
        _ = ((IPageExporter)document).ExportPages([0], SheetLayout.Default with { FitToPaper = true }, stream);
        using var flattened = Reopen(stream.ToArray(), out var path);
        try
        {
            return flattened.GetText(0, 0, flattened.GetCharacterCount(0));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
