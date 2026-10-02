// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.VisualTree;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks the shape and stamp tools: choosing them, drawing and placing, the sidebar list, undo and the tool bar.</summary>
public sealed class ShapeToolTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>A shape's first corner.</summary>
    private static readonly PagePoint From = new(100, 100);

    /// <summary>A shape's opposite corner.</summary>
    private static readonly PagePoint To = new(220, 180);

    /// <summary>Shapes and a stamp are added in the deeper tone of the chosen colour, listed, and undone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawsShapesAndPlacesStamps()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument("shapes.pdf", 1)]);
        var tab = main.SelectedTab!;
        var annotations = tab.Annotations;
        tab.SidebarMode = SidebarMode.Annotations;

        _ = await annotations.SetToolCommand.Execute(AnnotationTool.Arrow).ToTask();
        var label = annotations.ShapeLabel;
        var arrow = annotations.AddShape(0, AnnotationKind.Arrow, From, To);
        var box = annotations.AddShape(0, AnnotationKind.Rectangle, From, To);
        _ = await annotations.SetStampCommand.Execute("DRAFT").ToTask();
        var stampLabel = annotations.StampButtonLabel;
        var stamp = annotations.AddStamp(0, To);
        var kinds = annotations.Items.Select(static i => i.Annotation.Kind).ToArray();
        var color = annotations.Items.First(static i => i.Annotation.Kind == AnnotationKind.Rectangle).Annotation.Color;
        _ = await annotations.UndoCommand.Execute().ToTask();

        await Assert.That(label).IsEqualTo("Arrow");
        await Assert.That(stampLabel).IsEqualTo("Stamp: DRAFT");
        await Assert.That(arrow && box && stamp).IsTrue();
        await Assert.That(kinds).IsEquivalentTo([AnnotationKind.Arrow, AnnotationKind.Rectangle, AnnotationKind.Stamp]);
        await Assert.That(color).IsEqualTo(AnnotationColors.Deep(AnnotationColors.Sand));
        await Assert.That(annotations.Items.Count).IsEqualTo(kinds.Length - 1);
    }

    /// <summary>The shape and stamp buttons are on the annotation bar and every control on it has a name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsShapeAndStampButtons()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument("bar.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            _ = await main.SelectedTab!.Annotations.StartCommand.Execute().ToTask();
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<Avalonia.Controls.Button>().Any(static b => b.Name == "StampButton" && b.IsVisible));

            await Assert.That(window.GetVisualDescendants().OfType<Avalonia.Controls.Button>().Any(static b => b.Name == "ShapeButton" && b.IsVisible)).IsTrue();
            await Assert.That(string.Join(", ", AccessibilityTests.Unnamed(window))).IsEqualTo(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }
}
