// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Text;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// The PDFium engine's text box and image signature tests run against the native editor: formatted text in an embedded
/// subset or a built in font, read back, saved as typewriter free text and read again by PDFium.
/// </summary>
public sealed class NativeTextBoxTests
{
    /// <summary>The font size.</summary>
    private const float Size = 20;

    /// <summary>A wrap width that fits a few words.</summary>
    private const float Wrap = 120;

    /// <summary>How close positions must be, in points.</summary>
    private const float Tolerance = 0.5F;

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

    /// <summary>The pixel channels in BGRA.</summary>
    private const int Channels = 4;

    /// <summary>The width and height of the signature image.</summary>
    private const int ImageSize = 2;

    /// <summary>A sentence with an accent, a composite glyph and a kerning pair.</summary>
    private const string Sentence = "Hello é AV world";

    /// <summary>A short greeting.</summary>
    private const string Greeting = "Hello é";

    /// <summary>Where text boxes go.</summary>
    private static readonly PagePoint At = new(100, 150);

    /// <summary>The format in the test font.</summary>
    private static readonly TextFormat Installed = new(TestFont.Family, Size, Blue) { IsBold = true, IsUnderline = true, Alignment = TextBoxAlignment.Center };

    /// <summary>The signature image: opaque black, soft black, transparent paper and blue ink.</summary>
    private static readonly byte[] Ink = [0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0x7F, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0xFF];

    /// <summary>The signature rectangle.</summary>
    private static readonly PageRect Bounds = new(100, 300, 40, 40);

    /// <summary>A box in the test font is free text that keeps its text, format and place.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WritesAndReadsBack()
    {
        using var test = Open();
        var editor = test.Editor;
        var index = editor.AddTextBox(0, At, Wrap, Sentence, Installed);
        var annotation = NativeDocument.Read(editor, 0)[^1];
        var content = editor.GetTextBox(0, index);

        await Assert.That(index).IsGreaterThanOrEqualTo(0);
        await Assert.That(annotation.Kind).IsEqualTo(AnnotationKind.TextBox);
        await Assert.That(annotation.Contents).IsEqualTo(Sentence);
        await Assert.That(annotation.Color).IsEqualTo(Blue);
        await Assert.That(annotation.Bounds.Left).IsEqualTo(At.X).Within(Tolerance);
        await Assert.That(annotation.Bounds.Top).IsEqualTo(At.Y).Within(Tolerance);
        await Assert.That(annotation.Bounds.Width).IsEqualTo(Wrap).Within(Tolerance);
        await Assert.That(content!.Text).IsEqualTo(Sentence);
        await Assert.That(content.Format).IsEqualTo(Installed);
        await Assert.That(content.WrapWidth).IsEqualTo(Wrap);
        await Assert.That(editor.HasUnsavedChanges).IsTrue();
    }

    /// <summary>Saving writes typewriter free text with an embedded subset font and a text map, which PDFium reads back for editing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesAsFreeTextAndReopens()
    {
        using var test = Open();
        _ = test.Editor.AddTextBox(0, At, 0, Greeting, Installed);
        var saved = NativeDocument.Save(test.Editor);
        var dictionary = NativeDocument.Dictionaries(saved, 0).Single();
        var font = Font(dictionary);
        using var reopened = NativeDocument.OpenWithPdfium(saved, out var path);
        try
        {
            var annotations = NativeDocument.Read((IAnnotationEditor)reopened, 0);
            var content = ((ITextBoxEditor)reopened).GetTextBox(0, annotations[0].Index);

            await Assert.That(NativeDocument.Name(dictionary, KnownName.Subtype)).IsEqualTo("FreeText");
            await Assert.That(NativeDocument.Name(dictionary, KnownName.IT)).IsEqualTo("FreeTextTypeWriter");
            await Assert.That(dictionary.ContainsKey(KnownName.RC) && dictionary.ContainsKey(KnownName.DS) && dictionary.ContainsKey(KnownName.DA)).IsTrue();
            await Assert.That(font.IsName(KnownName.Subtype, KnownName.Type0)).IsTrue();
            await Assert.That(font.ContainsKey(KnownName.ToUnicode)).IsTrue();
            await Assert.That(font.GetArray(KnownName.DescendantFonts)!.GetDictionary(0)!.GetDictionary(KnownName.FontDescriptor)!.ContainsKey(KnownName.FontFile2)).IsTrue();
            await Assert.That(annotations.Count).IsEqualTo(1);
            await Assert.That(annotations[0].Kind).IsEqualTo(AnnotationKind.TextBox);
            await Assert.That(content!.Text).IsEqualTo(Greeting);
            await Assert.That(content.Format).IsEqualTo(Installed);
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
        var editor = test.Editor;
        var times = editor.AddTextBox(0, At, 0, "Plain", new("Times", Size, 0) { IsItalic = true });
        var mono = editor.AddTextBox(0, new(At.X, At.Y + Wrap), 0, "Missing family", new("No Such Mono", Size, 0));
        var fallback = editor.AddTextBox(0, new(At.X, At.Y + Wrap + Wrap), 0, "中 αβ", new("Helvetica", Size, 0));
        var dictionaries = NativeDocument.Dictionaries(NativeDocument.Save(editor), 0);

        await Assert.That(BaseFont(Font(dictionaries[times]))).IsEqualTo("Times-Italic");
        await Assert.That(BaseFont(Font(dictionaries[mono]))).IsEqualTo("Courier");
        await Assert.That(Font(dictionaries[fallback]).IsName(KnownName.Subtype, KnownName.Type0)).IsTrue();
    }

    /// <summary>A face whose licence forbids embedding is never embedded; its text uses the closest built in family.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeverEmbedsRestrictedFonts()
    {
        using var test = Open();
        var index = test.Editor.AddTextBox(0, At, 0, "Licensed", new(TestFont.LockedFamily, Size, 0));
        var dictionaries = NativeDocument.Dictionaries(NativeDocument.Save(test.Editor), 0);

        await Assert.That(BaseFont(Font(dictionaries[index]))).IsEqualTo("Helvetica");
    }

    /// <summary>Wrapped text grows downwards at its wrap width; comb text keeps the comb's width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrapsAndCombs()
    {
        using var test = Open();
        var editor = test.Editor;
        var one = editor.AddTextBox(0, At, 0, "short", Installed with { IsUnderline = false });
        var wrapped = editor.AddTextBox(0, At, Wrap, "a much longer piece of text that wraps", Installed with { IsUnderline = false });
        var comb = editor.AddTextBox(0, At, CombWidth, "AB123XYZ", Installed with { CombCells = Cells, Alignment = TextBoxAlignment.Left });
        var annotations = NativeDocument.Read(editor, 0);
        var oneBox = annotations.Single(a => a.Index == one).Bounds;
        var wrappedBox = annotations.Single(a => a.Index == wrapped).Bounds;
        var combBox = annotations.Single(a => a.Index == comb).Bounds;

        await Assert.That(wrappedBox.Height).IsGreaterThan(oneBox.Height * TwoLines);
        await Assert.That(wrappedBox.Width).IsEqualTo(Wrap).Within(Tolerance);
        await Assert.That(combBox.Width).IsEqualTo(CombWidth).Within(Tolerance);
        await Assert.That(editor.GetTextBox(0, comb)!.Format.CombCells).IsEqualTo(Cells);
    }

    /// <summary>Free text written by another program reads back with its text, size, colour, weight and alignment.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsOtherProgramsFreeText()
    {
        using var test = new NativeDocument(TestPdf.CreateWithFreeText());
        var annotations = NativeDocument.Read(test.Editor, 0);
        var content = test.Editor.GetTextBox(0, annotations[0].Index);

        await Assert.That(annotations[0].Kind).IsEqualTo(AnnotationKind.TextBox);
        await Assert.That(content!.Text).IsEqualTo(TestPdf.ForeignText);
        await Assert.That(content.Format.FontSize).IsEqualTo(ForeignSize);
        await Assert.That(content.Format.Color).IsEqualTo(Red);
        await Assert.That(content.Format.IsBold).IsTrue();
        await Assert.That(content.Format.Alignment).IsEqualTo(TextBoxAlignment.Center);
    }

    /// <summary>Blank text writes nothing; a removed text box cannot be read for editing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesBlankAndRemovedText()
    {
        using var test = Open();
        var editor = test.Editor;
        var blank = editor.AddTextBox(0, At, 0, "  \n ", Installed);
        var index = editor.AddTextBox(0, At, 0, "Gone", Installed);
        _ = editor.SetRemoved(0, index, true);

        await Assert.That(blank).IsEqualTo(-1);
        await Assert.That(editor.GetTextBox(0, index)).IsNull();
        await Assert.That(editor.GetTextBox(0, int.MaxValue)).IsNull();
    }

    /// <summary>The first baseline matches the PDFium engine's for the same text and format.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MeasuresTheFirstBaselineLikePdfium()
    {
        using var test = Open();
        using var pdfium = NativeDocument.OpenWithPdfium(TestPdf.Create(1), out var path);
        try
        {
            var expected = ((ITextBoxEditor)pdfium).GetFirstBaseline(Greeting, TextFormat.Default);

            await Assert.That(test.Editor.GetFirstBaseline(Greeting, TextFormat.Default)).IsEqualTo(expected).Within(Tolerance);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>An image signature is saved as a signature stamp with its bounds, and PDFium reads it back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesImageSignatures()
    {
        using var test = new NativeDocument(1);
        var index = test.Editor.AddImageSignature(0, Bounds, Ink, ImageSize, ImageSize);
        var reopened = NativeDocument.ReadWithPdfium(NativeDocument.Save(test.Editor), 0);
        var removed = test.Editor.Remove(0, index);

        await Assert.That(index).IsGreaterThanOrEqualTo(0);
        await Assert.That(reopened.Count).IsEqualTo(1);
        await Assert.That(reopened[0].Kind).IsEqualTo(AnnotationKind.Signature);
        await Assert.That(reopened[0].Bounds).IsEqualTo(Bounds);
        await Assert.That(removed).IsTrue();
        await Assert.That(NativeDocument.Read(test.Editor, 0)).IsEmpty();
    }

    /// <summary>Incomplete images, invalid sizes and missing pages fail without editing the document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsBadImageSignatures()
    {
        using var test = new NativeDocument(1);
        var editor = test.Editor;

        await Assert.That(() => editor.AddImageSignature(0, Bounds, [], ImageSize, ImageSize)).Throws<ArgumentException>();
        await Assert.That(() => editor.AddImageSignature(0, Bounds, Ink, 0, ImageSize)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(editor.AddImageSignature(1, Bounds, Ink, ImageSize, ImageSize)).IsEqualTo(-1);
        await Assert.That(editor.AddImageSignature(-1, Bounds, new byte[ImageSize * ImageSize * Channels], ImageSize, ImageSize)).IsEqualTo(-1);
        await Assert.That(editor.HasUnsavedChanges).IsFalse();
    }

    /// <summary>Opens a one page document whose text boxes may use the test fonts.</summary>
    /// <returns>The document.</returns>
    private static NativeDocument Open()
    {
        var test = new NativeDocument(1);
        test.Editor.FontCatalog = TestFont.Catalog;
        return test;
    }

    /// <summary>Gets the one font a saved text box's appearance uses.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The font dictionary.</returns>
    private static PdfDictionary Font(PdfDictionary annotation)
    {
        var fonts = PdfAnnotations.GetNormalAppearance(annotation)!.Dictionary.GetDictionary(KnownName.Resources)!.GetDictionary(KnownName.Font)!;
        return fonts.GetDictionary(fonts.GetKeyAt(0))!;
    }

    /// <summary>Gets a font's base font name.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The name.</returns>
    private static string BaseFont(PdfDictionary font) => NativeDocument.Name(font, KnownName.BaseFont);
}
