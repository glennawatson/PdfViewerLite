// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests for <see cref="AnnotationsViewModel"/>, saving and the unsaved-changes guard.</summary>
public sealed class AnnotationViewModelTests
{
    /// <summary>The page count of generated documents.</summary>
    private const int Pages = 3;

    /// <summary>Two annotations, or two questions.</summary>
    private const int Two = 2;

    /// <summary>Where the note goes.</summary>
    private static readonly PagePoint NoteAt = new(300, 300);

    /// <summary>Verifies marking text, adding a note through the prompt, the sidebar list, undo and saving.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotatesUndoesAndSaves()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var path = test.CreateDocument("annotate.pdf", Pages);
        main.Open([path]);
        var tab = main.SelectedTab!;
        var annotations = tab.Annotations;
        using var prompt = annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("Remember this"));
        var document = tab.TryGetDocument()!;
        var lines = new List<PageRect>();
        document.GetTextBounds(0, 0, TestPdf.Sentence.Length, lines);
        tab.SidebarMode = SidebarMode.Annotations;

        var marked = annotations.MarkText(AnnotationKind.Highlight, new Dictionary<int, List<PageRect>> { [0] = lines }, AnnotationColors.Sage);
        await annotations.AddNoteAsync(1, NoteAt);
        var afterAdding = annotations.Items.Count;
        var dirty = tab.HasUnsavedChanges;
        _ = await annotations.UndoCommand.Execute().ToTask();
        var afterUndo = annotations.Items.Count;
        var saved = tab.Save(path);

        await Assert.That(marked).IsTrue();
        await Assert.That(afterAdding).IsEqualTo(Two);
        await Assert.That(dirty).IsTrue();
        await Assert.That(afterUndo).IsEqualTo(1);
        await Assert.That(annotations.Items[0].Annotation.Kind).IsEqualTo(AnnotationKind.Highlight);
        await Assert.That(saved).IsTrue();
        await Assert.That(tab.HasUnsavedChanges).IsFalse();
    }

    /// <summary>Verifies closing a tab with unsaved edits asks first, and Cancel keeps it open.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsksBeforeDiscardingEdits()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument("guard.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var asked = 0;
        using var confirm = main.ConfirmInteraction.RegisterHandler(context =>
        {
            asked++;
            context.SetOutput(asked > 1);
        });
        var document = tab.TryGetDocument()!;
        var lines = new List<PageRect>();
        document.GetTextBounds(0, 0, TestPdf.Sentence.Length, lines);
        _ = tab.Annotations.MarkText(AnnotationKind.Underline, new Dictionary<int, List<PageRect>> { [0] = lines }, AnnotationColors.Slate);

        _ = await main.CloseTabCommand.Execute(tab).ToTask();
        var keptOpen = main.Tabs.Count;
        _ = await main.CloseTabCommand.Execute(tab).ToTask();

        await Assert.That(keptOpen).IsEqualTo(1);
        await Assert.That(main.HasTabs).IsFalse();
        await Assert.That(asked).IsEqualTo(Two);
    }
}
