// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Writing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>Linearized files: a first-page cross-reference section, a main section, and a hint stream.</summary>
public sealed class LinearizedTests
{
    /// <summary>The object number of the linearization dictionary.</summary>
    private const int LinearizationNumber = 7;

    /// <summary>The object number of the hint stream.</summary>
    private const int HintNumber = 8;

    /// <summary>The length of the hint stream's data.</summary>
    private const int HintLength = 24;

    /// <summary>The width of the first page.</summary>
    private const int FirstWidth = 200;

    /// <summary>The height of the first page.</summary>
    private const int FirstHeight = 100;

    /// <summary>Both layouts read without repair, with the right pages, contents and objects.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinearizedFilesReadWithoutRepair()
    {
        foreach (var streams in new[] { false, true })
        {
            using var store = PdfObjectStore.Open(LinearizedDocuments.Create(streams), null);
            var contents = WritingTestDocuments.PageContents(store);

            await Assert.That(store.WasRepaired).IsFalse();
            await Assert.That(store.UsesXrefStreams).IsEqualTo(streams);
            await Assert.That(WritingTestDocuments.CountMissing(store)).IsEqualTo(0);
            await Assert.That(Encoding.Latin1.GetString(contents[0])).IsEqualTo(LinearizedDocuments.FirstContent);
            await Assert.That(Encoding.Latin1.GetString(contents[1])).IsEqualTo(LinearizedDocuments.SecondContent);
        }
    }

    /// <summary>The pages come out in the order of the page tree, not the order of the file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinearizedPagesFollowThePageTree()
    {
        using var document = PdfDocumentReader.Open(LinearizedDocuments.Create(false), null);

        await Assert.That(document.PageCount).IsEqualTo(LinearizedDocuments.PageCount);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Width).IsEqualTo(FirstWidth);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Height).IsEqualTo(FirstHeight);
        await Assert.That(PdfDocumentPages.GetPage(document, 1).Width).IsEqualTo(FirstHeight);
        await Assert.That(PdfDocumentPages.GetPage(document, 1).Height).IsEqualTo(FirstWidth);
    }

    /// <summary>The linearization dictionary and hint stream are plain objects that read back and do no harm.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HintStreamAndLinearizationDictionaryRead()
    {
        foreach (var streams in new[] { false, true })
        {
            using var document = PdfDocumentReader.Open(LinearizedDocuments.Create(streams), null);
            var linearization = document.Objects.GetDictionary(new(LinearizationNumber, 0))!;
            var hint = document.Objects.GetObject(new(HintNumber, 0)).AsStream()!;

            await Assert.That(linearization.GetInt32(document.Objects.Names.Intern("Linearized"))).IsEqualTo(1);
            await Assert.That(linearization.GetInt32(KnownName.N)).IsEqualTo(LinearizedDocuments.PageCount);
            await Assert.That(hint.RawLength).IsEqualTo(HintLength);
            await Assert.That(DocumentExerciser.Read(document)).IsNotEmpty();
        }
    }

    /// <summary>An incremental update appended to a linearized file reads back with the edit and the original pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IncrementalUpdateOfLinearizedFileReadsBack()
    {
        foreach (var streams in new[] { false, true })
        {
            var original = LinearizedDocuments.Create(streams);
            using var store = PdfObjectStore.Open(original, null);
            var marker = store.Add(PdfValue.FromInteger(SaveReopenTests.MarkerValue));
            var catalogId = store.Trailer.GetRaw(KnownName.Root).AsReference();
            var catalog = store.Catalog.Clone();
            catalog.Set(store.Names.Intern(SaveReopenTests.MarkerKey), PdfValue.FromReference(marker));
            store.Replace(catalogId, PdfValue.FromDictionary(catalog));
            var saved = PdfIncrementalWriter.Save(store);
            using var reopened = PdfDocumentReader.Open(saved, null);

            await Assert.That(saved.AsSpan(0, original.Length).SequenceEqual(original)).IsTrue();
            await Assert.That(reopened.PageCount).IsEqualTo(LinearizedDocuments.PageCount);
            await Assert.That(reopened.Catalog.GetInteger(store.Names.Intern(SaveReopenTests.MarkerKey))).IsEqualTo(SaveReopenTests.MarkerValue);
        }
    }

    /// <summary>A linearized file with a spoiled startxref is rebuilt and still gives both pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinearizedFileWithBrokenStartXrefRepairs()
    {
        foreach (var streams in new[] { false, true })
        {
            var broken = Pdf2Documents.Replace(LinearizedDocuments.Create(streams), "startxref", "startxxxx");
            using var store = PdfObjectStore.Open(broken, null);
            var contents = WritingTestDocuments.PageContents(store);

            await Assert.That(store.WasRepaired).IsTrue();
            await Assert.That(contents.Count).IsEqualTo(LinearizedDocuments.PageCount);
            await Assert.That(Encoding.Latin1.GetString(contents[1])).IsEqualTo(LinearizedDocuments.SecondContent);
        }
    }

    /// <summary>A linearized file cut off after its first page, as in a partial download, still shows the first page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinearizedFileCutAfterFirstPageShowsFirstPage()
    {
        foreach (var streams in new[] { false, true })
        {
            var full = LinearizedDocuments.Create(streams);
            int end;
            using (var store = PdfObjectStore.Open(full, null))
            {
                end = store.GetDictionary(new(LinearizationNumber, 0))!.GetInt32(store.Names.Intern("E"));
            }

            using var document = PdfDocumentReader.Open(full.AsSpan(0, end).ToArray(), null);
            var content = PdfDocumentPages.GetPage(document, 0).Dictionary.GetStream(KnownName.Contents)!.DecodeToArray();

            await Assert.That(document.PageCount).IsGreaterThanOrEqualTo(1);
            await Assert.That(PdfDocumentPages.GetPage(document, 0).Width).IsEqualTo(FirstWidth);
            await Assert.That(Encoding.Latin1.GetString(content)).IsEqualTo(LinearizedDocuments.FirstContent);
        }
    }
}
