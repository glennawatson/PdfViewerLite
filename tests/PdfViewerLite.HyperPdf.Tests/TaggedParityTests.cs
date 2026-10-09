// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// Checks the managed structure reader gives the same tagged blocks as PDFium, and that those blocks reach the reading
/// view (and so the page's screen reader peer, which reads <see cref="ReadingDocument.Flatten"/>) in the same order.
/// </summary>
[NotInParallel]
public sealed class TaggedParityTests
{
    /// <summary>The corpus folder variable, as the compatibility tests read it.</summary>
    private const string CorpusVariable = "PDFVIEWERLITE_CORPUS_DIR";

    /// <summary>The PDFium test document is read the same way: kinds, levels, replacement text and characters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MatchesPdfiumOnTheTaggedTestDocument()
    {
        using var pair = new EnginePair(TestPdf.CreateTagged());

        await AssertSameBlocksAsync(pair.Pdfium, (HyperPdfDocument)pair.HyperPdf, 0);
    }

    /// <summary>Tables, lists, figures, replacement text, notes and unknown types are read as PDFium reads them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MatchesPdfiumOnMixedStructure()
    {
        using var pair = new EnginePair(TaggedParitySamples.Mixed());

        await AssertSameBlocksAsync(pair.Pdfium, (HyperPdfDocument)pair.HyperPdf, 0);
    }

    /// <summary>An untagged document gives no blocks on either engine.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntaggedDocumentsGiveNoBlocks()
    {
        using var pair = new EnginePair(TestPdf.Create(1));
        var blocks = new List<TaggedBlock>();

        await Assert.That(((HyperPdfDocument)pair.HyperPdf).GetTaggedBlocksNative(0, blocks)).IsFalse();
        await Assert.That(blocks.Count).IsEqualTo(0);
    }

    /// <summary>The reading view, which the page's screen reader peer speaks, gets the same text in the same order from both engines.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadingViewSpeaksTheSameText()
    {
        using var pair = new EnginePair(TestPdf.CreateTagged());
        var native = (HyperPdfDocument)pair.HyperPdf;
        var expected = new ReadingDocument((ITextLayoutSource)pair.Pdfium, pair.Pdfium.GetPageSizes()).GetPage(0);
        var actual = new ReadingDocument(new NativeTaggedSource(native), native.GetPageSizes()).GetPage(0);

        await Assert.That(actual.Blocks.All(static block => block.IsTagged)).IsTrue();
        await Assert.That(actual.Blocks.Select(static block => block.Text)).IsEquivalentTo(
            [TestPdf.TaggedHeading, TestPdf.TaggedFirst, TestPdf.TaggedSecond, TestPdf.TaggedFigure]);
        await Assert.That(ReadingDocument.Flatten(actual, out _)).IsEqualTo(ReadingDocument.Flatten(expected, out _));
    }

    /// <summary>Tagged documents from the PDF/UA corpus are read the same way on every page, when they are in the cache.</summary>
    /// <param name="id">The corpus document id.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("verapdf-ua-headings")]
    [Arguments("verapdf-ua-tables")]
    [Arguments("verapdf-ua-notes")]
    [Arguments("pyhanko-ua-and-a")]
    public async Task MatchesPdfiumOnCorpusDocuments(string id)
    {
        var path = Path.Combine(CorpusDirectory(), $"{id}.pdf");
        if (!File.Exists(path))
        {
            Skip.Test($"{id}.pdf is not in the corpus cache.");
        }

        using var pair = new EnginePair(await File.ReadAllBytesAsync(path));
        for (var page = 0; page < pair.Pdfium.PageCount; page++)
        {
            await AssertSameBlocksAsync(pair.Pdfium, (HyperPdfDocument)pair.HyperPdf, page);
        }
    }

    /// <summary>Gets the corpus cache folder.</summary>
    /// <returns>The folder.</returns>
    private static string CorpusDirectory() => Environment.GetEnvironmentVariable(CorpusVariable) is { Length: > 0 } configured
        ? configured
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "corpus");

    /// <summary>Checks both engines give the same blocks for a page.</summary>
    /// <param name="pdfium">The PDFium document.</param>
    /// <param name="native">The HyperPDF document.</param>
    /// <param name="pageIndex">The page.</param>
    /// <returns>A task.</returns>
    private static async Task AssertSameBlocksAsync(IDocument pdfium, HyperPdfDocument native, int pageIndex)
    {
        var expected = new List<TaggedBlock>();
        var actual = new List<TaggedBlock>();
        var expectedTagged = ((ITaggedStructureSource)pdfium).GetTaggedBlocks(pageIndex, expected);
        var actualTagged = native.GetTaggedBlocksNative(pageIndex, actual);

        await Assert.That(actualTagged).IsEqualTo(expectedTagged);
        await Assert.That(actual.Count).IsEqualTo(expected.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            await Assert.That(actual[i].Kind).IsEqualTo(expected[i].Kind);
            await Assert.That(actual[i].Level).IsEqualTo(expected[i].Level);
            await Assert.That(actual[i].ReplacementText).IsEqualTo(expected[i].ReplacementText);
            await Assert.That(actual[i].Characters).IsEquivalentTo(expected[i].Characters);
        }
    }
}
