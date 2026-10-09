// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Ocr;
using PdfViewerLite.Core.Text;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// The PDFium adapter's annotation, text box, image signature, text layer and save scenarios, run through the public
/// interfaces on both engines. Each run is checked against the PDFium suite's expectations and against PDFium's own
/// result for the same scenario.
/// </summary>
public sealed class EngineAnnotationSuiteTests
{
    /// <summary>The number of pages in the generated document.</summary>
    private const int PageCount = 2;

    /// <summary>Tolerance for positions, in points.</summary>
    private const float Tolerance = 2F;

    /// <summary>The text size in points.</summary>
    private const float FontSize = 12;

    /// <summary>The typed signature size in points.</summary>
    private const float SignatureSize = 24;

    /// <summary>The ink line width in points.</summary>
    private const float InkWidth = 2;

    /// <summary>The text box wrap width in points.</summary>
    private const float Wrap = 200;

    /// <summary>The side of the picture signature in pixels.</summary>
    private const int PictureSide = 8;

    /// <summary>A small inset into the highlight, in points.</summary>
    private const int Inset = 3;

    /// <summary>The recognition confidence given to text layer words.</summary>
    private const float Confidence = 90;

    /// <summary>The two annotations kept in the change scenario.</summary>
    private const int Two = 2;

    /// <summary>The text written in the text box.</summary>
    private const string BoxText = "Typed box";

    /// <summary>The marked line.</summary>
    private static readonly PageRect Line = new(72, 100, 200, 14);

    /// <summary>A drawn stroke.</summary>
    private static readonly PagePoint[] Stroke = [new(100, 300), new(140, 320), new(180, 300), new(220, 330)];

    /// <summary>Where the sticky note goes.</summary>
    private static readonly PagePoint NoteAt = new(400, 100);

    /// <summary>Where the text goes.</summary>
    private static readonly PagePoint TextAt = new(72, 500);

    /// <summary>Where the typed signature goes.</summary>
    private static readonly PagePoint SignatureAt = new(72, 600);

    /// <summary>Where the picture signature goes.</summary>
    private static readonly PageRect PictureAt = new(300, 600, 80, 40);

    /// <summary>The recognised word written into the text layer.</summary>
    private static readonly OcrWord Word = new("Recognised", new(300, 700, 380, 714), Confidence);

    /// <summary>Each kind is added and read back with its kind, colour, author and note, as on PDFium.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task AddsAndReadsEachKind(string engine)
    {
        var actual = Run(engine, AddEachKind);
        var expected = Run(EngineDocument.Pdfium, AddEachKind);
        AnnotationKind[] kinds =
        [
            AnnotationKind.Highlight, AnnotationKind.Underline, AnnotationKind.StrikeOut, AnnotationKind.Squiggly, AnnotationKind.Ink,
            AnnotationKind.Note, AnnotationKind.TextBox, AnnotationKind.Signature, AnnotationKind.Signature,
        ];

        await Assert.That(actual.Select(static a => a.Kind).ToArray()).IsEquivalentTo(kinds);
        await Assert.That(actual[0].Color).IsEqualTo(AnnotationColors.Sage);
        await Assert.That(actual[0].Contents).IsEqualTo("Check this");
        await Assert.That(actual[0].Author).IsEqualTo(Environment.UserName);
        await Assert.That(actual[0].Bounds.Left).IsEqualTo(Line.Left).Within(Tolerance);
        await Assert.That(actual[0].Bounds.Top).IsEqualTo(Line.Top).Within(Tolerance);
        await AssertSameAsync(actual, expected);
    }

    /// <summary>An annotation is recoloured, given a note and removed, as on PDFium.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task ChangesAndRemoves(string engine)
    {
        var actual = Run(engine, ChangeAndRemove);
        var expected = Run(EngineDocument.Pdfium, ChangeAndRemove);

        await Assert.That(actual.Count).IsEqualTo(Two);
        await Assert.That(actual[0].Color).IsEqualTo(AnnotationColors.Clay);
        await Assert.That(actual[0].Contents).IsEqualTo("Changed");
        await AssertSameAsync(actual, expected);
    }

    /// <summary>Replies thread under their comment with their author and review state, as on PDFium.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task ThreadsReplies(string engine)
    {
        var actual = Run(engine, Replies);
        var expected = Run(EngineDocument.Pdfium, Replies);

        await Assert.That(actual.Count).IsEqualTo(Two);
        await Assert.That(actual[0].Contents).IsEqualTo("Checked.");
        await Assert.That(actual[1].Contents).IsEqualTo("Accepted");
        await AssertSameAsync(actual, expected);
    }

    /// <summary>
    /// An annotation added after a page was drawn shows on the next drawing, survives saving, and is read back by both
    /// engines; saving clears the unsaved state.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task DrawsSavesAndReopens(string engine)
    {
        using var test = new EngineDocument(engine, TestPdf.Create(PageCount));
        var editor = (IAnnotationEditor)test.Document;
        var before = IsHighlighted(test.Document);
        var index = editor.AddMarkup(1, AnnotationKind.Highlight, [Line], AnnotationColors.Sand, "Saved note");
        var unsaved = editor.HasUnsavedChanges;
        var drawn = IsHighlighted(test.Document);
        var bytes = Save(editor);
        var saved = !editor.HasUnsavedChanges;

        await Assert.That(before).IsFalse();
        await Assert.That(index).IsGreaterThanOrEqualTo(0);
        await Assert.That(unsaved).IsTrue();
        await Assert.That(drawn).IsTrue();
        await Assert.That(saved).IsTrue();
        foreach (var reader in TestEngines.All())
        {
            using var reopened = new EngineDocument(reader, bytes);
            var annotations = Read((IAnnotationEditor)reopened.Document, 1);

            await Assert.That(annotations.Count).IsEqualTo(1);
            await Assert.That(annotations[0].Contents).IsEqualTo("Saved note");
            await Assert.That(IsHighlighted(reopened.Document)).IsTrue();
        }
    }

    /// <summary>An annotation removed but kept is left out of the saved file and can still be restored, as on PDFium.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task LeavesRemovedAnnotationsOutOfSaves(string engine)
    {
        using var test = new EngineDocument(engine, TestPdf.Create(PageCount));
        var editor = (IAnnotationEditor)test.Document;
        var index = editor.AddMarkup(0, AnnotationKind.Highlight, [Line], AnnotationColors.Sand, string.Empty);
        var removed = editor.SetRemoved(0, index, true);
        var hidden = Read(editor, 0).Count;
        using var reopened = new EngineDocument(EngineDocument.Pdfium, Save(editor));
        var restored = editor.SetRemoved(0, index, false);

        await Assert.That(removed).IsTrue();
        await Assert.That(hidden).IsEqualTo(0);
        await Assert.That(Read((IAnnotationEditor)reopened.Document, 0).Count).IsEqualTo(0);
        await Assert.That(restored).IsTrue();
        await Assert.That(Read(editor, 0).Count).IsEqualTo(1);
        await Assert.That(editor.HasUnsavedChanges).IsTrue();
    }

    /// <summary>A text box in a built in font keeps its text, format and place, as on PDFium.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task WritesAndReadsTextBoxes(string engine)
    {
        using var test = new EngineDocument(engine, TestPdf.Create(1));
        var editor = (ITextBoxEditor)test.Document;
        var format = new TextFormat("Helvetica", FontSize, AnnotationColors.Ink);
        var index = editor.AddTextBox(0, TextAt, Wrap, BoxText, format);
        var content = editor.GetTextBox(0, index);
        var annotations = Read((IAnnotationEditor)test.Document, 0);
        using var baseline = EngineDocument.Open(EngineDocument.Pdfium, test.FilePath);
        var expectedBaseline = ((ITextBoxEditor)baseline).GetFirstBaseline(BoxText, format);

        await Assert.That(index).IsGreaterThanOrEqualTo(0);
        await Assert.That(annotations[^1].Kind).IsEqualTo(AnnotationKind.TextBox);
        await Assert.That(annotations[^1].Bounds.Left).IsEqualTo(TextAt.X).Within(Tolerance);
        await Assert.That(annotations[^1].Bounds.Top).IsEqualTo(TextAt.Y).Within(Tolerance);
        await Assert.That(content!.Text).IsEqualTo(BoxText);
        await Assert.That(content.Format).IsEqualTo(format);
        await Assert.That(content.WrapWidth).IsEqualTo(Wrap);
        await Assert.That(editor.GetFirstBaseline(BoxText, format)).IsEqualTo(expectedBaseline).Within(Tolerance);
    }

    /// <summary>A picture signature is added as a signature annotation with the chosen bounds, as on PDFium.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task AddsPictureSignatures(string engine)
    {
        var actual = Run(engine, AddPicture);
        var expected = Run(EngineDocument.Pdfium, AddPicture);

        await Assert.That(actual.Count).IsEqualTo(1);
        await Assert.That(actual[0].Bounds.Left).IsEqualTo(PictureAt.Left).Within(Tolerance);
        await Assert.That(actual[0].Bounds.Width).IsEqualTo(PictureAt.Width).Within(Tolerance);
        await AssertSameAsync(actual, expected);
    }

    /// <summary>A recognised word written into the text layer becomes page text, found by search on both engines.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task WritesTheTextLayer(string engine)
    {
        using var test = new EngineDocument(engine, TestPdf.Create(1));
        var document = test.Document;
        var before = document.GetCharacterCount(0);
        var written = ((ITextLayerWriter)document).AddTextLayer(0, [Word]);
        var matches = new List<TextMatch>();
        document.Find(0, Word.Text, SearchOptions.None, matches);
        using var reopened = new EngineDocument(EngineDocument.Pdfium, Save((IAnnotationEditor)document));
        var reopenedMatches = new List<TextMatch>();
        reopened.Document.Find(0, Word.Text, SearchOptions.None, reopenedMatches);

        await Assert.That(written).IsEqualTo(1);
        await Assert.That(document.GetCharacterCount(0)).IsGreaterThan(before);
        await Assert.That(matches.Count).IsEqualTo(1);
        await Assert.That(reopenedMatches.Count).IsEqualTo(1);
    }

    /// <summary>The chosen author is recorded on new annotations, and a blank name falls back to the user name.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(TestEngines), nameof(TestEngines.All))]
    public async Task RecordsTheChosenAuthor(string engine)
    {
        using var test = new EngineDocument(engine, TestPdf.Create(1));
        var editor = (IAnnotationEditor)test.Document;
        editor.Author = "  Reviewer  ";
        _ = editor.AddNote(0, NoteAt, "Signed off", AnnotationColors.Sand);
        var chosen = editor.Author;
        editor.Author = " ";

        await Assert.That(chosen).IsEqualTo("Reviewer");
        await Assert.That(Read(editor, 0)[0].Author).IsEqualTo("Reviewer");
        await Assert.That(editor.Author).IsEqualTo(Environment.UserName);
    }

    /// <summary>Runs a scenario on a fresh two page document opened with an engine.</summary>
    /// <param name="engine">The engine.</param>
    /// <param name="scenario">The scenario, which edits and reads back the annotations.</param>
    /// <returns>What the scenario read.</returns>
    private static List<Observed> Run(string engine, Func<IDocument, List<Observed>> scenario)
    {
        using var test = new EngineDocument(engine, TestPdf.Create(PageCount));
        return scenario(test.Document);
    }

    /// <summary>Adds one of each kind and reads them back.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The annotations.</returns>
    private static List<Observed> AddEachKind(IDocument document)
    {
        var editor = (IAnnotationEditor)document;
        int[] strokes = [Stroke.Length];
        _ = editor.AddMarkup(0, AnnotationKind.Highlight, [Line], AnnotationColors.Sage, "Check this");
        _ = editor.AddMarkup(0, AnnotationKind.Underline, [Line], AnnotationColors.Slate, string.Empty);
        _ = editor.AddMarkup(0, AnnotationKind.StrikeOut, [Line], AnnotationColors.Clay, string.Empty);
        _ = editor.AddMarkup(0, AnnotationKind.Squiggly, [Line], AnnotationColors.Sand, string.Empty);
        _ = editor.AddInk(0, Stroke, strokes, AnnotationColors.Ink, InkWidth, AnnotationKind.Ink);
        _ = editor.AddNote(0, NoteAt, "A sticky note", AnnotationColors.Sand);
        _ = editor.AddText(0, TextAt, "Written on the page\nSecond line", FontSize, AnnotationColors.Ink, AnnotationKind.TextBox);
        _ = editor.AddText(0, SignatureAt, "Glenn Watson", SignatureSize, AnnotationColors.Ink, AnnotationKind.Signature);
        _ = editor.AddInk(0, Stroke, strokes, AnnotationColors.Ink, InkWidth, AnnotationKind.Signature);
        return Observe(editor, 0);
    }

    /// <summary>Recolours, notes and removes annotations, then reads back what is left.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The annotations.</returns>
    private static List<Observed> ChangeAndRemove(IDocument document)
    {
        var editor = (IAnnotationEditor)document;
        var index = editor.AddMarkup(0, AnnotationKind.Highlight, [Line], AnnotationColors.Sand, string.Empty);
        var text = editor.AddText(0, TextAt, "Text", FontSize, AnnotationColors.Ink, AnnotationKind.TextBox);
        var gone = editor.AddNote(0, NoteAt, "Gone", AnnotationColors.Sage);
        _ = editor.SetColor(0, index, AnnotationColors.Clay);
        _ = editor.SetColor(0, text, AnnotationColors.Slate);
        _ = editor.SetContents(0, index, "Changed");
        _ = editor.Remove(0, gone);
        return Observe(editor, 0);
    }

    /// <summary>Adds a note with two replies and reads the replies back.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The replies, as observed annotations.</returns>
    private static List<Observed> Replies(IDocument document)
    {
        var editor = (IAnnotationEditor)document;
        var note = editor.AddNote(0, NoteAt, "Is this right?", AnnotationColors.Sand);
        _ = editor.AddReply(0, note, "Checked.", ReviewState.None);
        _ = editor.AddReply(0, note, "Accepted", ReviewState.Accepted);
        var replies = new List<AnnotationReply>();
        editor.GetReplies(0, note, replies);
        return [.. replies.Select(static reply => new Observed(AnnotationKind.Note, (uint)reply.State, reply.Contents, reply.Author, default))];
    }

    /// <summary>Adds a picture signature and reads it back.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The annotations.</returns>
    private static List<Observed> AddPicture(IDocument document)
    {
        var pixels = new byte[PictureSide * PictureSide * PagePixels.BytesPerPixel];
        pixels.AsSpan().Fill(byte.MaxValue);
        _ = ((IImageSignatureEditor)document).AddImageSignature(0, PictureAt, pixels, PictureSide, PictureSide);
        return Observe((IAnnotationEditor)document, 0);
    }

    /// <summary>Reads a page's annotations as observations.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="page">The page.</param>
    /// <returns>The observations.</returns>
    private static List<Observed> Observe(IAnnotationEditor editor, int page) =>
        [.. Read(editor, page).Select(static a => new Observed(a.Kind, a.Color, a.Contents, a.Author, a.Bounds))];

    /// <summary>Reads a page's annotations.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="page">The page.</param>
    /// <returns>The annotations.</returns>
    private static List<PageAnnotation> Read(IAnnotationEditor editor, int page)
    {
        var annotations = new List<PageAnnotation>();
        editor.GetAnnotations(page, annotations);
        return annotations;
    }

    /// <summary>Saves an editor's document into memory.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Save(IAnnotationEditor editor)
    {
        using var stream = new MemoryStream();
        _ = editor.Save(stream);
        return stream.ToArray();
    }

    /// <summary>Renders page 2 with annotations and checks the middle of the highlighted line is tinted.</summary>
    /// <param name="document">The document.</param>
    /// <returns><see langword="true"/> when tinted.</returns>
    private static bool IsHighlighted(IDocument document)
    {
        var page = new PagePixels(document, 1, RenderFlags.Annotations);
        return page.IsTinted((int)(Line.Left + (Line.Width / Two)), (int)Line.Top + Inset);
    }

    /// <summary>Checks two observation lists match, with positions within the tolerance.</summary>
    /// <param name="actual">The engine's observations.</param>
    /// <param name="expected">PDFium's observations.</param>
    /// <returns>A task.</returns>
    private static async Task AssertSameAsync(List<Observed> actual, List<Observed> expected)
    {
        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(actual[i] with { Bounds = default }).IsEqualTo(expected[i] with { Bounds = default });
            await Assert.That(actual[i].Bounds.Left).IsEqualTo(expected[i].Bounds.Left).Within(Tolerance);
            await Assert.That(actual[i].Bounds.Top).IsEqualTo(expected[i].Bounds.Top).Within(Tolerance);
            await Assert.That(actual[i].Bounds.Width).IsEqualTo(expected[i].Bounds.Width).Within(Tolerance);
            await Assert.That(actual[i].Bounds.Height).IsEqualTo(expected[i].Bounds.Height).Within(Tolerance);
        }
    }

    /// <summary>What a scenario read back from an annotation or reply.</summary>
    /// <param name="Kind">The kind.</param>
    /// <param name="Color">The colour, or a reply's review state.</param>
    /// <param name="Contents">The note text.</param>
    /// <param name="Author">The author.</param>
    /// <param name="Bounds">The bounds in page space.</param>
    [DebuggerDisplay("Observed: {Kind} {Contents}")]
    private sealed record Observed(AnnotationKind Kind, uint Color, string Contents, string Author, PageRect Bounds);
}
