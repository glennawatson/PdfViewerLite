// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for <see cref="PdfEditTransaction"/> and <see cref="PdfEditHistory"/>.</summary>
public sealed class EditTransactionTests
{
    /// <summary>Pages in the test document.</summary>
    private const int Pages = 3;

    /// <summary>The last page's index.</summary>
    private const int LastPage = 2;

    /// <summary>A quarter turn.</summary>
    private const int QuarterTurn = 90;

    /// <summary>A small undo depth.</summary>
    private const int SmallDepth = 2;

    /// <summary>Edits made in steps.</summary>
    private const int Steps = 4;

    /// <summary>Undo restores the exact objects, the page cache follows, and redo applies the edit again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndoRestoresExactObjectsAndPages()
    {
        using var document = PdfDocument.Open(EditingTestDocuments.CreateStructured(), null);
        var firstId = document.GetPage(0).Id;

        // An edit made before any transaction is the state undo must give back, instance for instance.
        var edited = document.GetPage(0).Dictionary.Clone();
        document.Objects.Replace(firstId, PdfValue.FromDictionary(edited));
        document.ReorderPages([LastPage, 1, 0, Pages, Pages + 1, Pages + LastPage]);
        document.DeletePages([1]);

        await Assert.That(document.History.UndoCount).IsEqualTo(LastPage);
        await Assert.That(document.History.UndoLabel).IsEqualTo("Delete pages");
        await Assert.That(document.Undo()).IsTrue();
        await Assert.That(document.Undo()).IsTrue();
        await Assert.That(document.Undo()).IsFalse();

        await Assert.That(document.Objects.GetDictionary(firstId)).IsSameReferenceAs(edited);
        await Assert.That(document.Objects.GetEditedNumbers()).IsEquivalentTo((int[])[firstId.Number]);
        await Assert.That(document.Catalog.GetDictionary(KnownName.Pages)!.GetArray(KnownName.Kids)!.Count).IsEqualTo(LastPage);
        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("1 2 3 4 5 6"));
        await Assert.That(document.GetPage(0).Dictionary).IsSameReferenceAs(edited);
        await Assert.That(document.GetOutline().Count).IsEqualTo(LastPage);

        await Assert.That(document.Redo()).IsTrue();
        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("3 2 1 4 5 6"));
        await Assert.That(document.History.RedoCount).IsEqualTo(1);
    }

    /// <summary>Operations inside one transaction commit and undo together; a new commit clears redo.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OperationsJoinTheOpenTransaction()
    {
        using var document = PdfDocument.Open(EditingTestDocuments.CreateFlat(Pages), null);
        using (var transaction = document.BeginEdit("Tidy"))
        {
            document.ReorderPages([LastPage, 1, 0]);
            document.SetRotation(0, QuarterTurn);
            document.SetMetadata(new() { Title = "Tidied" });
            await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("3 2 1"));
            transaction.Commit();
            await Assert.That(transaction.Kinds).IsEqualTo(PdfChangeKinds.PageChanges | PdfChangeKinds.Metadata);
        }

        await Assert.That(document.History.UndoCount).IsEqualTo(1);
        await Assert.That(document.GetInfo().Title).IsEqualTo("Tidied");
        _ = document.Undo();
        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("1 2 3"));
        await Assert.That(document.GetPage(LastPage).Rotation).IsEqualTo(0);
        await Assert.That(document.GetInfo().Title).IsNull();
        await Assert.That(document.Objects.Trailer.ContainsKey(KnownName.Info)).IsFalse();

        document.SetRotation(1, QuarterTurn);
        await Assert.That(document.History.RedoCount).IsEqualTo(0);
    }

    /// <summary>Disposing an open transaction rolls back every change, including ones made directly on the store.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposeRollsBack()
    {
        using var document = PdfDocument.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var catalogId = document.Objects.Trailer.GetRaw(KnownName.Root).AsReference();
        using (var transaction = document.BeginEdit("Abandoned"))
        {
            document.DeletePages([0]);
            var added = document.Objects.Add(PdfValue.FromInteger(Pages));
            document.Objects.Delete(catalogId);
            await Assert.That(document.Objects.GetObject(added).AsInt32()).IsEqualTo(Pages);
            await Assert.That(transaction.ChangedCount).IsGreaterThan(1);
        }

        await Assert.That(document.Objects.HasEdits).IsFalse();
        await Assert.That(document.PageCount).IsEqualTo(Pages);
        await Assert.That(document.Objects.CurrentTransaction).IsNull();
        await Assert.That(document.History.UndoCount).IsEqualTo(0);
        await Assert.That(PdfIncrementalWriter.Save(document.Objects).Length).IsGreaterThan(0);
    }

    /// <summary>A failed operation rolls back what it had done.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FailedOperationLeavesNoTrace()
    {
        using var document = PdfDocument.Open(EditingTestDocuments.CreateFlat(Pages), null);

        await Assert.That(() => document.MovePages([0, 0], 0)).Throws<ArgumentException>();
        await Assert.That(document.Objects.HasEdits).IsFalse();
        await Assert.That(document.Objects.CurrentTransaction).IsNull();
    }

    /// <summary>The undo stack keeps at most its depth, dropping the oldest, and undo is refused while a transaction is open.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DepthBoundsTheUndoStack()
    {
        using var document = PdfDocument.Open(EditingTestDocuments.CreateFlat(Pages), null);
        document.History.Depth = SmallDepth;
        for (var i = 0; i < Steps; i++)
        {
            document.RotatePages([0], QuarterTurn);
        }

        await Assert.That(document.History.UndoCount).IsEqualTo(SmallDepth);
        _ = document.Undo();
        _ = document.Undo();
        await Assert.That(document.Undo()).IsFalse();
        await Assert.That(document.GetPage(0).Rotation).IsEqualTo(QuarterTurn + QuarterTurn);

        using var transaction = document.BeginEdit("Open");
        await Assert.That(() => document.Redo()).Throws<InvalidOperationException>();
        await Assert.That(() => document.BeginEdit("Second")).Throws<InvalidOperationException>();
    }

    /// <summary>Undo and redo from many threads keep the store consistent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConcurrentReadersSeeWholeStates()
    {
        using var document = PdfDocument.Open(EditingTestDocuments.CreateFlat(Pages), null);
        document.ReorderPages([LastPage, 1, 0]);
        using var cancellation = new CancellationTokenSource();
        var reader = Task.Run(
            () =>
            {
                var bad = 0;
                while (!cancellation.IsCancellationRequested)
                {
                    bad += document.PageCount == Pages ? 0 : 1;
                }

                return bad;
            },
            CancellationToken.None);
        for (var i = 0; i < Steps; i++)
        {
            _ = document.Undo();
            _ = document.Redo();
        }

        await cancellation.CancelAsync();
        await Assert.That(await reader).IsEqualTo(0);
        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("3 2 1"));
    }
}
