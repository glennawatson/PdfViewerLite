// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Writing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for the page operations on <see cref="PdfDocument"/>.</summary>
public sealed class PageOperationTests
{
    /// <summary>A quarter turn.</summary>
    private const int QuarterTurn = 90;

    /// <summary>A half turn.</summary>
    private const int HalfTurn = 180;

    /// <summary>Three quarter turns.</summary>
    private const int ThreeQuarterTurn = 270;

    /// <summary>A rotation that is not normalised.</summary>
    private const int UnnormalisedTurn = 450;

    /// <summary>The third page's index.</summary>
    private const int ThirdPage = 2;

    /// <summary>The fourth page's index.</summary>
    private const int FourthPage = 3;

    /// <summary>The fifth page's index.</summary>
    private const int FifthPage = 4;

    /// <summary>The last page's index.</summary>
    private const int LastPage = 5;

    /// <summary>The position in the parent tree's /Nums of the value for key 1.</summary>
    private const int SecondParentTreeValue = 3;

    /// <summary>The entries of a /Names array holding one pair.</summary>
    private const int OneNamePair = 2;

    /// <summary>Pages in a small flat document.</summary>
    private const int FlatPages = 3;

    /// <summary>Gets the reversed order of the structured document.</summary>
    private static int[] Reversed => [LastPage, FifthPage, FourthPage, ThirdPage, 1, 0];

    /// <summary>Reordering rewrites a flat tree, pushes inherited boxes and rotation down, and keeps labels with their pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReorderKeepsInheritedAttributesAndLabels()
    {
        using var document = PdfDocument.Open(EditingTestDocuments.CreateStructured(), null);
        document.ReorderPages(Reversed);

        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("6 5 4 3 2 1"));
        await Assert.That(document.GetPage(LastPage).Rotation).IsEqualTo(EditingTestDocuments.InheritedRotation);
        await Assert.That(document.GetPage(0).Rotation).IsEqualTo(0);
        await Assert.That(document.GetPage(0).MediaBox.Width).IsEqualTo(EditingTestDocuments.WideWidth);
        await Assert.That(document.GetPage(LastPage).MediaBox.Width).IsEqualTo(EditingTestDocuments.TallWidth);
        await Assert.That(document.GetPage(0).Resources).IsNotNull();
        await Assert.That(document.GetPageLabel(0)).IsEqualTo("A-4");
        await Assert.That(document.GetPageLabel(FourthPage)).IsEqualTo("A-1");
        await Assert.That(document.GetPageLabel(FifthPage)).IsEqualTo("ii");
        await Assert.That(document.GetPageLabel(LastPage)).IsEqualTo("i");
        await AssertFlatTree(document);
    }

    /// <summary>The page tree survives an incremental save and a compact rewrite, and old nodes are freed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReorderSurvivesBothWriters()
    {
        using var document = PdfDocument.Open(EditingTestDocuments.CreateStructured(), null);
        document.ReorderPages(Reversed);
        using var incremental = PdfDocument.Open(PdfIncrementalWriter.Save(document.Objects), null);
        using var compact = PdfDocument.Open(PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), null);

        foreach (var saved in (PdfDocument[])[incremental, compact])
        {
            await Assert.That(EditingTestDocuments.PageTexts(saved)).IsEquivalentTo(EditingTestDocuments.Expected("6 5 4 3 2 1"));
            await Assert.That(saved.GetPage(LastPage).Rotation).IsEqualTo(EditingTestDocuments.InheritedRotation);
            await Assert.That(saved.GetPageLabel(0)).IsEqualTo("A-4");
            await AssertFlatTree(saved);
        }

        await Assert.That(incremental.Objects.GetObject(new(EditingTestDocuments.FirstNodeNumber, 0)).IsNull).IsTrue();
    }

    /// <summary>Moving follows FPDF_MovePages: the moved pages, in the order given, start at the destination.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovePagesPlacesThemAtTheDestination()
    {
        using var document = PdfDocument.Open(EditingTestDocuments.CreateStructured(), null);
        document.MovePages([LastPage, 0], 1);

        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("2 6 1 3 4 5"));
    }

    /// <summary>Deleting drops outline leaves and named destinations that led to the page; tags still resolve.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteDropsDestinationsAndKeepsTags()
    {
        using var document = PdfDocument.Open(EditingTestDocuments.CreateStructured(), null);
        document.DeletePages([0]);

        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("2 3 4 5 6"));
        var outline = document.GetOutline();
        await Assert.That(outline.Count).IsEqualTo(1);
        await Assert.That(outline[0].Title).IsEqualTo("Two");
        await Assert.That(document.Catalog.GetDictionary(KnownName.Outlines)!.GetInt32(KnownName.Count)).IsEqualTo(1);
        await Assert.That(document.Catalog.GetDictionary(KnownName.Dests)!.Count).IsEqualTo(1);
        await Assert.That(document.Catalog.GetDictionary(KnownName.Names)!.GetDictionary(KnownName.Dests)!.GetArray(KnownName.Names)!.Count).IsEqualTo(OneNamePair);
        await Assert.That(document.GetPageLabel(0)).IsEqualTo("ii");

        // The second element names a page that is still in the document, and its parent tree entry still resolves.
        var element = document.Objects.GetDictionary(new(EditingTestDocuments.SecondElementNumber, 0))!;
        await Assert.That(document.GetPageIndex(element.GetRaw(KnownName.Pg).AsReference())).IsEqualTo(0);
        var parentTree = document.Catalog.GetDictionary(KnownName.StructTreeRoot)!.GetDictionary(KnownName.ParentTree)!.GetArray(KnownName.Nums)!;
        await Assert.That(parentTree.GetArray(SecondParentTreeValue)!.GetDictionary(0)).IsSameReferenceAs(element);
        await Assert.That(document.GetPage(0).Dictionary.GetInt32(KnownName.StructParents)).IsEqualTo(1);
    }

    /// <summary>Deleting every page is refused and changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeletingEveryPageIsRefused()
    {
        using var document = PdfDocument.Open(EditingTestDocuments.CreateFlat(FlatPages), null);

        await Assert.That(() => document.DeletePages([0, 1, ThirdPage])).Throws<ArgumentException>();
        await Assert.That(document.PageCount).IsEqualTo(FlatPages);
        await Assert.That(document.Objects.HasEdits).IsFalse();
    }

    /// <summary>Rotation is set absolutely or turned relatively, normalised to quarter turns.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RotationIsNormalised()
    {
        using var document = PdfDocument.Open(EditingTestDocuments.CreateStructured(), null);
        document.SetRotation(FourthPage, UnnormalisedTurn);
        document.RotatePages([0, FourthPage], -QuarterTurn);

        await Assert.That(document.GetPage(FourthPage).Rotation).IsEqualTo(0);
        await Assert.That(document.GetPage(0).Rotation).IsEqualTo(0);
        document.RotatePages([1], QuarterTurn);
        await Assert.That(document.GetPage(1).Rotation).IsEqualTo(HalfTurn);
        document.SetRotation(1, -QuarterTurn);
        await Assert.That(document.GetPage(1).Rotation).IsEqualTo(ThreeQuarterTurn);
    }

    /// <summary>Inserted pages keep their content, boxes and rotation, and links between them name the copies.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InsertCopiesPagesFromAnotherDocument()
    {
        using var source = PdfDocument.Open(EditingTestDocuments.CreateStructured(), null);
        using var target = PdfDocument.Open(EditingTestDocuments.CreateFlat(FlatPages), null);
        target.InsertPages(1, source, [0, LastPage]);

        await Assert.That(EditingTestDocuments.PageTexts(target)).IsEquivalentTo(EditingTestDocuments.Expected("1 1 6 2 3"));
        await Assert.That(target.GetPage(1).Rotation).IsEqualTo(EditingTestDocuments.InheritedRotation);
        await Assert.That(target.GetPage(ThirdPage).MediaBox.Width).IsEqualTo(EditingTestDocuments.WideWidth);
        await Assert.That(target.GetPage(1).Dictionary.ContainsKey(KnownName.StructParents)).IsFalse();
        await Assert.That(target.GetPage(1).Resources!.GetDictionary(KnownName.Font)).IsNotNull();

        using var saved = PdfDocument.Open(PdfIncrementalWriter.Save(target.Objects), null);
        await Assert.That(EditingTestDocuments.PageTexts(saved)).IsEquivalentTo(EditingTestDocuments.Expected("1 1 6 2 3"));
        await Assert.That(WritingTestDocuments.CountMissing(saved.Objects)).IsEqualTo(0);
    }

    /// <summary>Pages can be inserted from the same document, as copies.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InsertFromItselfDuplicatesPages()
    {
        using var document = PdfDocument.Open(EditingTestDocuments.CreateFlat(FlatPages), null);
        document.InsertPages(FlatPages, document, [0]);

        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("1 2 3 1"));
        await Assert.That(document.GetPage(FlatPages).Id).IsNotEqualTo(document.GetPage(0).Id);
    }

    /// <summary>Extracting pages writes a new document with just those pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtractWritesTheChosenPages()
    {
        using var document = PdfDocument.Open(EditingTestDocuments.CreateStructured(), null);
        using var extracted = PdfDocument.Open(document.ExtractPages([FourthPage, 0]), null);

        await Assert.That(EditingTestDocuments.PageTexts(extracted)).IsEquivalentTo(EditingTestDocuments.Expected("4 1"));
        await Assert.That(extracted.GetPage(1).Rotation).IsEqualTo(EditingTestDocuments.InheritedRotation);
    }

    /// <summary>An encrypted document stays encrypted, and its reordered pages read back with both writers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EncryptedDocumentStaysEncrypted()
    {
        using var document = PdfDocument.Open(WritingTestDocuments.Encrypt(EditingTestDocuments.CreateFlat(FlatPages)), null);
        document.ReorderPages([ThirdPage, 0, 1]);
        document.SetRotation(0, QuarterTurn);
        using var incremental = PdfDocument.Open(PdfIncrementalWriter.Save(document.Objects), null);
        using var compact = PdfDocument.Open(PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Classic), null);

        foreach (var saved in (PdfDocument[])[incremental, compact])
        {
            await Assert.That(saved.IsEncrypted).IsTrue();
            await Assert.That(EditingTestDocuments.PageTexts(saved)).IsEquivalentTo(EditingTestDocuments.Expected("3 1 2"));
            await Assert.That(saved.GetPage(0).Rotation).IsEqualTo(QuarterTurn);
        }
    }

    /// <summary>Checks the page tree is one root whose kids are the pages, each pointing back at it.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    private static async Task AssertFlatTree(PdfDocument document)
    {
        var rootRef = document.Catalog.GetRaw(KnownName.Pages).AsReference();
        var root = document.Catalog.GetDictionary(KnownName.Pages)!;
        await Assert.That(root.GetInt32(KnownName.Count)).IsEqualTo(document.PageCount);
        await Assert.That(root.GetArray(KnownName.Kids)!.Count).IsEqualTo(document.PageCount);
        await Assert.That(root.ContainsKey(KnownName.Resources)).IsFalse();
        for (var i = 0; i < document.PageCount; i++)
        {
            var page = document.GetPage(i).Dictionary;
            await Assert.That(page.GetRaw(KnownName.Parent).AsReference().Number).IsEqualTo(rootRef.Number);
            await Assert.That(page.ContainsKey(KnownName.MediaBox)).IsTrue();
        }
    }
}
