// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Printing;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.Core.Signatures;
using PdfViewerLite.Core.Signatures.Signing;
using PdfViewerLite.Core.Speech;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Compatibility.Tests;

/// <summary>
/// Opens, navigates, searches, annotates, fills, signs, saves, prints, reflows and prepares for reading aloud every
/// document in the real-world corpus.
/// </summary>
public sealed class RealWorldPdfTests
{
    /// <summary>The thumbnail width pages are rendered at.</summary>
    private const int ThumbnailWidth = 96;

    /// <summary>Bytes per BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>A page with at least this many characters counts as a text page.</summary>
    private const int TextPageCharacters = 200;

    /// <summary>The most pages searched for a text page, reflowed or printed in one test.</summary>
    private const int PageLimit = 40;

    /// <summary>The shortest word searched for.</summary>
    private const int SearchWordLength = 6;

    /// <summary>The lowest share of transcript paragraphs that must be found.</summary>
    private const double MinimumCoverage = 0.6;

    /// <summary>The lowest share of neighbouring transcript paragraphs that must keep their order.</summary>
    private const double MinimumOrder = 0.75;

    /// <summary>Halves a length or a size.</summary>
    private const int Half = 2;

    /// <summary>The pages put on each sheet when printing several per sheet.</summary>
    private const int PagesPerSheet = 4;

    /// <summary>A note and a highlight.</summary>
    private const int NoteAndHighlight = 2;

    /// <summary>The text typed into form fields, short enough for fields with a maximum length.</summary>
    private const string TypedText = "Corpus 26";

    /// <summary>Opens the document, or fails cleanly when it is listed as unreadable.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(RealWorldPdfs), nameof(RealWorldPdfs.All))]
    public async Task Opens(RealWorldPdf document)
    {
        var path = await RealWorldPdfCache.GetAsync(document);
        if (document.Unreadable)
        {
            await Assert.That(() => new PdfiumEngine().Open(path, document.Password)).Throws<DocumentOpenException>();
            return;
        }

        using var opened = new PdfiumEngine().Open(path, document.Password);
        var sizes = opened.GetPageSizes();

        await Assert.That(opened.PageCount).IsGreaterThan(0);
        await Assert.That(sizes.Length).IsEqualTo(opened.PageCount);
        await Assert.That(Array.TrueForAll(sizes, static s => s.Width > 0 && s.Height > 0)).IsTrue();
        _ = opened.GetMetadata();
    }

    /// <summary>A password protected document asks for its password and opens with it.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(RealWorldPdfs), nameof(RealWorldPdfs.Protected))]
    public async Task AsksForThePassword(RealWorldPdf document)
    {
        var path = await RealWorldPdfCache.GetAsync(document);

        var error = await Assert.That(() => new PdfiumEngine().Open(path, null)).Throws<DocumentOpenException>();
        await Assert.That(error!.Error).IsEqualTo(DocumentOpenError.Password);
        using var opened = new PdfiumEngine().Open(path, document.Password);
        await Assert.That(opened.PageCount).IsGreaterThan(0);
    }

    /// <summary>Every page renders, and a document with text draws some ink.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(RealWorldPdfs), nameof(RealWorldPdfs.Readable))]
    public async Task RendersEveryPage(RealWorldPdf document)
    {
        using var opened = await OpenAsync(document);
        var sizes = opened.GetPageSizes();
        var rendered = 0;
        var inked = 0;
        for (var page = 0; page < opened.PageCount; page++)
        {
            var scale = ThumbnailWidth / Math.Max(sizes[page].Width, sizes[page].Height);
            var width = Math.Max(1, (int)Math.Ceiling(sizes[page].Width * scale));
            var height = Math.Max(1, (int)Math.Ceiling(sizes[page].Height * scale));
            var pixels = new byte[width * height * BytesPerPixel];
            Array.Fill(pixels, byte.MaxValue);
            if (!opened.Render(new(page, scale, PageRotation.None, 0, 0, RenderFlags.Annotations), new(pixels, width, height, width * BytesPerPixel)))
            {
                continue;
            }

            rendered++;
            inked += HasInk(pixels) ? 1 : 0;
        }

        // A damaged file may have pages that cannot be read at all; the rest must still render.
        await Assert.That(rendered).IsEqualTo(document.Has("damaged") ? Math.Max(1, rendered) : opened.PageCount);
        await Assert.That(inked).IsGreaterThan(0);
    }

    /// <summary>The outline, page labels and links can be read, and internal links point at real pages.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(RealWorldPdfs), nameof(RealWorldPdfs.Readable))]
    public async Task Navigates(RealWorldPdf document)
    {
        using var opened = await OpenAsync(document);
        var badTargets = new List<string>();
        CheckOutline(opened.GetOutline(), opened.PageCount, badTargets);
        for (var page = 0; page < opened.PageCount; page++)
        {
            _ = opened.GetPageLabel(page);
            foreach (var link in opened.GetLinks(page))
            {
                if (link.Target.Kind == LinkTargetKind.Page && (link.Target.PageIndex < 0 || link.Target.PageIndex >= opened.PageCount))
                {
                    badTargets.Add($"link on page {page + 1} to {link.Target.PageIndex + 1}");
                }
            }
        }

        await Assert.That(string.Join(", ", badTargets)).IsEqualTo(string.Empty);
    }

    /// <summary>Search finds a word taken from the document's own text.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(RealWorldPdfs), nameof(RealWorldPdfs.Readable))]
    public async Task FindsItsOwnText(RealWorldPdf document)
    {
        using var opened = await OpenAsync(document);
        if (FindTextPage(opened) is not { } page || PickWord(opened.GetText(page, 0, opened.GetCharacterCount(page))) is not { } word)
        {
            return;
        }

        var matches = new List<TextMatch>();
        opened.Find(page, word, SearchOptions.None, matches);

        await Assert.That(matches.Count).IsGreaterThan(0);
        await Assert.That(opened.GetText(page, matches[0].Start, matches[0].Length)).IsEqualTo(word).IgnoringCase();
    }

    /// <summary>A highlight and a note can be added, saved and read back.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(RealWorldPdfs), nameof(RealWorldPdfs.Readable))]
    public async Task AnnotatesAndSaves(RealWorldPdf document)
    {
        using var opened = await OpenAsync(document);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(opened, typeof(IAnnotationEditor))!;
        var page = FindTextPage(opened) ?? 0;
        var before = Annotations(editor, page).Count;
        var size = opened.GetPageSizes()[page];
        _ = editor.AddNote(page, new(size.Width / Half, size.Height / Half), "Checked by the corpus tests", AnnotationColors.Sand);
        var bounds = new List<PageRect>();
        if (opened.GetCharacterCount(page) > 0)
        {
            opened.GetTextBounds(page, 0, Math.Min(opened.GetCharacterCount(page), SearchWordLength), bounds);
            _ = editor.AddMarkup(page, AnnotationKind.Highlight, bounds.ToArray(), AnnotationColors.Sand, string.Empty);
        }

        var added = Annotations(editor, page).Count - before;
        using var saved = TempFile.Create();
        await using (var stream = File.Create(saved.Path))
        {
            await Assert.That(editor.Save(stream)).IsTrue();
        }

        using var reopened = new PdfiumEngine().Open(saved.Path, document.Password);

        await Assert.That(added).IsEqualTo(bounds.Count > 0 ? NoteAndHighlight : 1);
        await Assert.That(reopened.PageCount).IsEqualTo(opened.PageCount);
        await Assert.That(Annotations((IAnnotationEditor)DocumentFeatures.CastFeature(reopened, typeof(IAnnotationEditor))!, page).Count).IsEqualTo(before + added);
    }

    /// <summary>Text fields and check boxes can be filled, saved and read back.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(RealWorldPdfs), nameof(RealWorldPdfs.Forms))]
    public async Task FillsAndSaves(RealWorldPdf document)
    {
        using var opened = await OpenAsync(document);
        var filler = (IFormFiller)DocumentFeatures.CastFeature(opened, typeof(IFormFiller))!;
        var filled = new List<FormField>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var page = 0; page < opened.PageCount && page < PageLimit; page++)
        {
            Fill(filler, page, names, filled);
        }

        using var saved = TempFile.Create();
        await using (var stream = File.Create(saved.Path))
        {
            _ = ((IAnnotationEditor)DocumentFeatures.CastFeature(opened, typeof(IAnnotationEditor))!).Save(stream);
        }

        using var reopened = new PdfiumEngine().Open(saved.Path, document.Password);
        var lost = Lost((IFormFiller)DocumentFeatures.CastFeature(reopened, typeof(IFormFiller))!, filled);

        await Assert.That(filler.HasForm).IsTrue();
        await Assert.That(string.Join(", ", lost)).IsEqualTo(string.Empty);
    }

    /// <summary>The document can be signed with a certificate and the new signature checks out.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(RealWorldPdfs), nameof(RealWorldPdfs.Readable))]
    public async Task SignsAndVerifies(RealWorldPdf document)
    {
        if (document.Password is not null || document.Has("encrypted"))
        {
            // Save encrypted documents without encryption before certificate signing.
            return;
        }

        var path = await RealWorldPdfCache.GetAsync(document);
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        using var signed = TempFile.Create();
        PdfSigner.Sign(path, signed.Path, certificate, new(0, "Compatibility check", "Corpus", TimeProvider.System.GetUtcNow()));

        using var reopened = new PdfiumEngine().Open(signed.Path, null);
        var signatures = ((ISignatureSource)DocumentFeatures.CastFeature(reopened, typeof(ISignatureSource))!).GetSignatures();
        var mine = SignatureVerifier.Verify(signatures[^1], signed.Path, [certificate]);

        await Assert.That(reopened.PageCount).IsGreaterThan(0);
        await Assert.That(mine.Integrity).IsEqualTo(SignatureIntegrity.Intact);
        await Assert.That(mine.IsTrusted).IsTrue();
    }

    /// <summary>Signatures made by other software are found and their integrity checked.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(RealWorldPdfs), nameof(RealWorldPdfs.Signed))]
    public async Task ChecksExistingSignatures(RealWorldPdf document)
    {
        var path = await RealWorldPdfCache.GetAsync(document);
        using var opened = new PdfiumEngine().Open(path, document.Password);
        var signatures = ((ISignatureSource)DocumentFeatures.CastFeature(opened, typeof(ISignatureSource))!).GetSignatures();
        var results = signatures.Select(s => SignatureVerifier.Verify(s, path, [])).ToList();

        await Assert.That(signatures.Count).IsGreaterThan(0);
        await Assert.That(results.TrueForAll(static r => r.Integrity != SignatureIntegrity.Unknown)).IsTrue();
    }

    /// <summary>Pages can be printed: exported for the printer, one per sheet and four per sheet.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(RealWorldPdfs), nameof(RealWorldPdfs.Readable))]
    public async Task Prints(RealWorldPdf document)
    {
        using var opened = await OpenAsync(document);
        var count = Math.Min(opened.PageCount, PageLimit);
        var pages = Enumerable.Range(0, count).ToArray();
        using var single = TempFile.Create();
        using var four = TempFile.Create();
        await using (var stream = File.Create(single.Path))
        {
            await Assert.That(((IPageExporter)DocumentFeatures.CastFeature(opened, typeof(IPageExporter))!).ExportPages(pages, SheetLayout.Default, stream)).IsTrue();
        }

        await using (var stream = File.Create(four.Path))
        {
            await Assert.That(((IPageExporter)DocumentFeatures.CastFeature(opened, typeof(IPageExporter))!).ExportPages(pages, new(PagesPerSheet, PaperSize.Letter, true), stream)).IsTrue();
        }

        using var printed = new PdfiumEngine().Open(single.Path, null);
        using var sheets = new PdfiumEngine().Open(four.Path, null);

        // Pages a damaged file cannot load are left out, so the readable ones still print.
        var expected = document.Has("damaged") ? Math.Clamp(printed.PageCount, 1, count) : count;
        await Assert.That(printed.PageCount).IsEqualTo(expected);
        await Assert.That(sheets.PageCount).IsEqualTo((expected + PagesPerSheet - 1) / PagesPerSheet);
    }

    /// <summary>Text pages reflow into blocks for Focus Mode and split into sentences for Read Aloud.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(RealWorldPdfs), nameof(RealWorldPdfs.Readable))]
    public async Task ReflowsAndSplitsSentences(RealWorldPdf document)
    {
        using var opened = await OpenAsync(document);
        if (FindTextPage(opened) is not { } page)
        {
            return;
        }

        var reading = new ReadingDocument((ITextLayoutSource)DocumentFeatures.CastFeature(opened, typeof(ITextLayoutSource))!, opened.GetPageSizes());
        var flat = ReadingDocument.Flatten(reading.GetPage(page), out var map);
        var sentences = new List<SpeechSentence>();
        SentenceSplitter.Split(flat, sentences);
        var spoken = sentences.Select(s => SentenceSplitter.ToSpeech(flat.AsSpan(s.Start, s.Length))).Where(static s => s.Length > 0).ToList();

        await Assert.That(flat.Length).IsGreaterThan(TextPageCharacters / Half);
        await Assert.That(map.Length).IsEqualTo(flat.Length);
        await Assert.That(Array.TrueForAll(map, i => i >= -1 && i < opened.GetCharacterCount(page))).IsTrue();
        await Assert.That(spoken.Count).IsGreaterThan(0);
    }

    /// <summary>The reading order follows the document's transcript: columns, captions and footnotes in sequence.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(typeof(RealWorldPdfs), nameof(RealWorldPdfs.Transcribed))]
    public async Task ReadsInTranscriptOrder(RealWorldPdf document)
    {
        using var opened = await OpenAsync(document);
        var transcript = await RealWorldPdfCache.GetGroundTruthAsync(document);
        var reading = new ReadingDocument((ITextLayoutSource)DocumentFeatures.CastFeature(opened, typeof(ITextLayoutSource))!, opened.GetPageSizes());
        var text = new StringBuilder();
        for (var page = 0; page < opened.PageCount && page < PageLimit; page++)
        {
            _ = text.Append(ReadingDocument.Flatten(reading.GetPage(page), out _)).Append("\n\n");
        }

        var score = ReadingOrderScore.Measure(transcript, text.ToString());

        await Assert.That(score.Coverage).IsGreaterThanOrEqualTo(MinimumCoverage).Because(score.ToString());
        await Assert.That(score.Order).IsGreaterThanOrEqualTo(MinimumOrder).Because(score.ToString());
    }

    /// <summary>Opens a corpus document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The opened document.</returns>
    private static async Task<IDocument> OpenAsync(RealWorldPdf document) =>
        new PdfiumEngine().Open(await RealWorldPdfCache.GetAsync(document), document.Password);

    /// <summary>Finds the first page with a fair amount of text.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The page, or <see langword="null"/> when the document has little text.</returns>
    private static int? FindTextPage(IDocument document)
    {
        for (var page = 0; page < document.PageCount && page < PageLimit; page++)
        {
            if (document.GetCharacterCount(page) >= TextPageCharacters)
            {
                return page;
            }
        }

        return null;
    }

    /// <summary>Picks a word made only of letters from the middle of a page's text.</summary>
    /// <param name="text">The page text.</param>
    /// <returns>The word, or <see langword="null"/> when there is none.</returns>
    private static string? PickWord(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(static w => w.Length >= SearchWordLength && w.All(char.IsLetter))
            .ToList();
        return words.Count == 0 ? null : words[words.Count / Half];
    }

    /// <summary>Determines whether a rendered page has anything but white on it.</summary>
    /// <param name="pixels">The BGRA pixels.</param>
    /// <returns><see langword="true"/> when some pixel is not white.</returns>
    private static bool HasInk(byte[] pixels) => pixels.AsSpan().IndexOfAnyExcept(byte.MaxValue) >= 0;

    /// <summary>Checks every outline entry that points at a page points at a real one.</summary>
    /// <param name="nodes">The outline level.</param>
    /// <param name="pageCount">The number of pages.</param>
    /// <param name="bad">Collects the bad entries.</param>
    private static void CheckOutline(IReadOnlyList<OutlineNode> nodes, int pageCount, List<string> bad)
    {
        foreach (var node in nodes)
        {
            if (node.Target.Kind == LinkTargetKind.Page && (node.Target.PageIndex < 0 || node.Target.PageIndex >= pageCount))
            {
                bad.Add($"outline '{node.Title}' to {node.Target.PageIndex + 1}");
            }

            CheckOutline(node.Children, pageCount, bad);
        }
    }

    /// <summary>Types into a page's text fields and flips its check boxes.</summary>
    /// <param name="filler">The form filler.</param>
    /// <param name="page">The page.</param>
    /// <param name="names">The fields already changed; widgets sharing a field change once.</param>
    /// <param name="filled">Collects the fields that changed.</param>
    private static void Fill(IFormFiller filler, int page, HashSet<string> names, List<FormField> filled)
    {
        foreach (var field in Fields(filler, page))
        {
            var changed = !field.IsReadOnly && names.Add(field.Name) && field.Kind switch
            {
                FormFieldKind.Text => filler.SetText(page, field.Index, TypedText),
                FormFieldKind.CheckBox => filler.SetChecked(page, field.Index, !field.IsChecked),
                _ => false,
            };
            if (changed)
            {
                filled.Add(field);
            }
        }
    }

    /// <summary>Lists the filled fields whose new values did not survive saving.</summary>
    /// <param name="reopened">The saved document.</param>
    /// <param name="filled">The fields as they were before filling.</param>
    /// <returns>The names of the fields that lost their values.</returns>
    private static List<string> Lost(IFormFiller reopened, List<FormField> filled)
    {
        var lost = new List<string>();
        foreach (var field in filled)
        {
            var now = Fields(reopened, field.PageIndex).Find(f => f.Index == field.Index);
            var kept = field.Kind == FormFieldKind.Text ? now?.Value == TypedText : now?.IsChecked == !field.IsChecked;
            if (!kept)
            {
                lost.Add(field.Name);
            }
        }

        return lost;
    }

    /// <summary>Lists a page's annotations.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="page">The page.</param>
    /// <returns>The annotations.</returns>
    private static List<PageAnnotation> Annotations(IAnnotationEditor editor, int page)
    {
        var list = new List<PageAnnotation>();
        editor.GetAnnotations(page, list);
        return list;
    }

    /// <summary>Lists a page's form fields.</summary>
    /// <param name="filler">The form filler.</param>
    /// <param name="page">The page.</param>
    /// <returns>The fields.</returns>
    private static List<FormField> Fields(IFormFiller filler, int page)
    {
        var list = new List<FormField>();
        filler.GetFields(page, list);
        return list;
    }
}
