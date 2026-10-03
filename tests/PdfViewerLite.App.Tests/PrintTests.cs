// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.Services;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Platform;
using PdfViewerLite.Pdfium;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests for printing through the desktop's print service.</summary>
public sealed class PrintTests
{
    /// <summary>The pages in the range test document.</summary>
    private const int RangePages = 6;

    /// <summary>The index of two pages per sheet in the choices.</summary>
    private const int TwoPerSheet = 1;

    /// <summary>Four pages at two per sheet.</summary>
    private const int ExpectedSheets = 2;

    /// <summary>How many times to check a condition.</summary>
    private const int WaitSteps = 200;

    /// <summary>The pause between checks.</summary>
    private static readonly TimeSpan WaitStep = TimeSpan.FromMilliseconds(25);

    /// <summary>Where the note is added.</summary>
    private static readonly PagePoint NoteAt = new(100, 100);

    /// <summary>Verifies the printed copy carries the annotations, is handed over with the file name, and is removed afterwards.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PrintsCopyWithAnnotations()
    {
        var printer = new RecordingPrinter();
        using var test = new TestServices(new PrintingPlatform(printer));
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument("print.pdf", 1)]);
        var tab = main.SelectedTab!;
        _ = ((IAnnotationEditor)tab.TryGetDocument()!).AddNote(0, NoteAt, "Printed note", AnnotationColors.Sand);
        using var handler = tab.PrintPreviewInteraction.RegisterHandler(ConfirmWhenReadyAsync);

        _ = await tab.PrintCommand.Execute().ToTask();

        await Assert.That(printer.Title).IsEqualTo("print.pdf");
        await Assert.That(printer.AnnotationCount).IsEqualTo(1);
        await Assert.That(File.Exists(printer.Path)).IsFalse();
        await Assert.That(tab.Notice).IsNull();
    }

    /// <summary>Verifies a desktop without printing says so plainly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SaysWhenPrintingIsUnavailable()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument("noprint.pdf", 1)]);
        var tab = main.SelectedTab!;
        using var handler = tab.PrintPreviewInteraction.RegisterHandler(ConfirmWhenReadyAsync);

        _ = await tab.PrintCommand.Execute().ToTask();

        await Assert.That(tab.Notice).IsEqualTo("Printing is not available on this desktop. Choose Save as PDF instead.");
    }

    /// <summary>Verifies a custom range and pages per sheet change what prints, shown before printing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreviewsChosenPages()
    {
        var printer = new RecordingPrinter();
        using var test = new TestServices(new PrintingPlatform(printer));
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument("range.pdf", RangePages)]);
        var tab = main.SelectedTab!;
        var sheets = 0;
        using var handler = tab.PrintPreviewInteraction.RegisterHandler(async context =>
        {
            var preview = context.Input;
            preview.PageChoice = PrintPageChoice.Custom;
            preview.CustomPages = "1-3, 5";
            preview.PagesPerSheetIndex = TwoPerSheet;
            _ = await WaitAsync(() => preview.IsValid && !preview.IsBuilding && preview.Summary == "2 sheets");
            sheets = preview.Sheets.Count;
            context.SetOutput(true);
        });

        _ = await tab.PrintCommand.Execute().ToTask();

        await Assert.That(sheets).IsEqualTo(ExpectedSheets);
        await Assert.That(printer.PageCount).IsEqualTo(ExpectedSheets);
    }

    /// <summary>Confirms the preview once its sheets are ready.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private static async Task ConfirmWhenReadyAsync(IInteractionContext<PrintPreviewViewModel, bool> context)
    {
        var preview = context.Input;
        _ = await WaitAsync(() => preview.IsValid && !preview.IsBuilding);
        context.SetOutput(true);
    }

    /// <summary>Waits for a condition.</summary>
    /// <param name="condition">The condition.</param>
    /// <returns><see langword="true"/> when it held in time.</returns>
    private static async Task<bool> WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < WaitSteps && !condition(); i++)
        {
            await Task.Delay(WaitStep);
        }

        return condition();
    }

    /// <summary>A print service that opens the file it is given and counts its annotations.</summary>
    private sealed class RecordingPrinter : IPrintService
    {
        /// <summary>Gets the file handed over.</summary>
        public string? Path { get; private set; }

        /// <summary>Gets the job title.</summary>
        public string? Title { get; private set; }

        /// <summary>Gets the page count of the handed over file.</summary>
        public int PageCount { get; private set; }

        /// <summary>Gets the annotations found on the first page of the handed over file.</summary>
        public int AnnotationCount { get; private set; }

        /// <inheritdoc/>
        public bool IsAvailable => true;

        /// <inheritdoc/>
        public Task<bool> PrintAsync(string filePath, string title, CancellationToken cancellationToken)
        {
            Path = filePath;
            Title = title;
            using var document = new PdfiumEngine().Open(filePath, null);
            PageCount = document.PageCount;
            List<PageAnnotation> annotations = [];
            ((IAnnotationEditor)document).GetAnnotations(0, annotations);
            AnnotationCount = annotations.Count;
            return Task.FromResult(true);
        }
    }

    /// <summary>A desktop with nothing but a print service.</summary>
    /// <param name="printer">The print service.</param>
    private sealed class PrintingPlatform(IPrintService printer) : IDesktopPlatform
    {
        /// <summary>The integration printing is added to.</summary>
        private readonly FallbackPlatform _fallback = new();

        /// <inheritdoc/>
        public string Name => "Test";

        /// <inheritdoc/>
        public IDesktopThemeSource? ThemeSource => null;

        /// <inheritdoc/>
        public IFileManagerLauncher FileManager => _fallback.FileManager;

        /// <inheritdoc/>
        public IRecentDocumentStore RecentDocuments => _fallback.RecentDocuments;

        /// <inheritdoc/>
        public IPrintService Printer => printer;

        /// <inheritdoc/>
        public string? GetLaunchActivationToken() => null;

        /// <inheritdoc/>
        public Task<bool> TryForwardAsync(OpenRequest request) => Task.FromResult(false);

        /// <inheritdoc/>
        public Task<ISingleInstance?> TryClaimSingleInstanceAsync() => Task.FromResult<ISingleInstance?>(null);
    }
}
