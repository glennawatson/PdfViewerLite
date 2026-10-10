// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Reading;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Checks tagged documents are read in their logical order, with artifacts left out.</summary>
public sealed class TaggedStructureTests
{
    /// <summary>The structure tree gives the blocks, their kinds and heading level, in logical order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsTheStructureTree()
    {
        using var file = new TaggedFile(TestPdf.CreateTagged());
        using var document = new PdfiumEngine().Open(file.Path, null);
        var blocks = new List<TaggedBlock>();

        var tagged = ((ITaggedStructureSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(ITaggedStructureSource))!).GetTaggedBlocks(0, blocks);

        await Assert.That(tagged).IsTrue();
        await Assert.That(blocks.Select(static b => b.Kind)).IsEquivalentTo(
            [ReadingBlockKind.Heading, ReadingBlockKind.Paragraph, ReadingBlockKind.Paragraph, ReadingBlockKind.Figure]);
        await Assert.That(blocks[0].Level).IsEqualTo(1);
        await Assert.That(blocks[^1].ReplacementText).IsEqualTo(TestPdf.TaggedFigure);
    }

    /// <summary>
    /// The reading order follows the tags, not the drawing order or position, and the running header marked as an
    /// artifact is not read.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsInLogicalOrderWithoutArtifacts()
    {
        using var file = new TaggedFile(TestPdf.CreateTagged());
        using var document = new PdfiumEngine().Open(file.Path, null);
        var reading = new ReadingDocument((ITextLayoutSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(ITextLayoutSource))!, document.GetPageSizes());

        var page = reading.GetPage(0);

        await Assert.That(page.Blocks.Select(static b => b.Text)).IsEquivalentTo(
            [TestPdf.TaggedHeading, TestPdf.TaggedFirst, TestPdf.TaggedSecond, TestPdf.TaggedFigure]);
        await Assert.That(page.Blocks.All(static b => b.IsTagged)).IsTrue();
        await Assert.That(page.Blocks[0].Level).IsEqualTo(1);
    }

    /// <summary>Each tagged block's characters map back to the page, so highlighting follows the reading.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MapsTaggedTextBackToThePage()
    {
        using var file = new TaggedFile(TestPdf.CreateTagged());
        using var document = new PdfiumEngine().Open(file.Path, null);
        var reading = new ReadingDocument((ITextLayoutSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(ITextLayoutSource))!, document.GetPageSizes());
        var first = reading.GetPage(0).Blocks[1];
        var start = Array.Find(first.CharIndices, static i => i >= 0);

        await Assert.That(document.GetText(0, start, TestPdf.TaggedFirst.Length)).IsEqualTo(TestPdf.TaggedFirst);
    }

    /// <summary>An untagged document has no structure, so the layout is used.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntaggedDocumentsUseTheLayout()
    {
        using var file = new TaggedFile(TestPdf.Create(1));
        using var document = new PdfiumEngine().Open(file.Path, null);
        var blocks = new List<TaggedBlock>();

        await Assert.That(((ITaggedStructureSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(ITaggedStructureSource))!).GetTaggedBlocks(0, blocks)).IsFalse();
        await Assert.That(blocks.Count).IsEqualTo(0);
    }

    /// <summary>A generated PDF written to a temporary file, deleted on dispose.</summary>
    private sealed class TaggedFile : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TaggedFile"/> class.</summary>
        /// <param name="bytes">The PDF.</param>
        internal TaggedFile(byte[] bytes)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tagged-{Guid.NewGuid():N}.pdf");
            File.WriteAllBytes(Path, bytes);
        }

        /// <summary>Gets the path.</summary>
        internal string Path { get; }

        /// <inheritdoc/>
        public void Dispose() => File.Delete(Path);
    }
}
