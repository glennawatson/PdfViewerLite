// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Platform;
using PdfViewerLite.Core.Printing;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// The print window, modelled on a browser's print preview: the sheets that will print on the left, built from the very
/// file that is printed, and a few settings on the right. Printing hands that file to the desktop's print dialog, which
/// picks the printer; Save as PDF writes it where the user chooses.
/// </summary>
[DebuggerDisplay("{Summary}")]
public sealed class PrintPreviewViewModel : ReactiveObject, IDisposable
{
    /// <summary>The most copies offered.</summary>
    private const int MaxCopies = 999;

    /// <summary>The Save as PDF destination.</summary>
    private static readonly PrintTarget SaveAsPdfTarget = new(PrintDestination.SaveAsPdf, string.Empty, "Save as PDF");

    /// <summary>The system print dialog destination.</summary>
    private static readonly PrintTarget SystemDialogTarget = new(PrintDestination.SystemDialog, string.Empty, "System print dialog");

    /// <summary>The tab being printed.</summary>
    private readonly DocumentTabViewModel _tab;

    /// <summary>The application services.</summary>
    private readonly AppServices _services;

    /// <summary>The subscriptions rebuilding the preview.</summary>
    private readonly MultipleDisposable _subscriptions;

    /// <summary>Raises <see cref="Confirmed"/>.</summary>
    private readonly Signal<RxVoid> _confirmed = new();

    /// <summary>The pages chosen, reused between rebuilds.</summary>
    private readonly List<int> _chosen = [];

    /// <summary>Counts rebuilds, so a slow one that finishes late is dropped.</summary>
    private int _version;

    /// <summary>The preview document.</summary>
    private IDocument? _preview;

    /// <summary>Initializes a new instance of the <see cref="PrintPreviewViewModel"/> class.</summary>
    /// <param name="tab">The tab being printed.</param>
    /// <param name="services">The application services.</param>
    public PrintPreviewViewModel(DocumentTabViewModel tab, AppServices services)
    {
        _tab = tab;
        _services = services;
        var ready = this.WhenAnyValue(static vm => vm.IsValid, static vm => vm.IsBuilding, static (valid, building) => valid && !building);
        ConfirmCommand = ReactiveCommand.Create(() => _confirmed.OnNext(RxVoid.Default), ready);
        SystemDialogCommand = ReactiveCommand.Create(() =>
        {
            SelectedTarget = SystemDialogTarget;
            _confirmed.OnNext(RxVoid.Default);
        });
        SelectedTarget = SystemDialogTarget;
        _ = LoadPrintersAsync();
        _subscriptions =
        [
            this.WhenAnyValue(
                    static vm => vm.PageChoice,
                    static vm => vm.CustomPages,
                    static vm => vm.PagesPerSheetIndex,
                    static vm => vm.Paper,
                    static vm => vm.IncludeAnnotations,
                    static (_, _, _, _, _) => RxVoid.Default)
                .SubscribeSafe(OnSettingsChanged, static error => Trace.TraceError(error.ToString())),
        ];
    }

    /// <summary>Gets the document's file name, for the window title.</summary>
    public string FileName => _tab.FileName;

    /// <summary>Gets the destinations: the printers (the default first), Save as PDF and the system dialog.</summary>
    public ObservableCollection<PrintTarget> Targets { get; } = [SaveAsPdfTarget, SystemDialogTarget];

    /// <summary>Gets or sets the chosen destination.</summary>
    public PrintTarget? SelectedTarget
    {
        get;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(Destination));
            this.RaisePropertyChanged(nameof(ShowsPaper));
        }
    }

    /// <summary>Gets where the print goes.</summary>
    public PrintDestination Destination => SelectedTarget?.Kind ?? PrintDestination.SaveAsPdf;

    /// <summary>Gets the job settings for the chosen printer.</summary>
    public PrintJobOptions JobOptions => new(SelectedTarget?.Name ?? string.Empty, Copies, Colour, TwoSided, Paper);

    /// <summary>Gets or sets the number of copies sent to a printer.</summary>
    public int Copies
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, Math.Clamp(value, 1, MaxCopies));
    } = 1;

    /// <summary>Gets or sets a value indicating whether a printer prints in colour rather than black and white.</summary>
    public bool Colour
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    /// <summary>Gets or sets a value indicating whether a printer prints on both sides of the paper.</summary>
    public bool TwoSided
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets or sets which pages print.</summary>
    public PrintPageChoice PageChoice
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets or sets the typed page ranges, used when <see cref="PageChoice"/> is custom.</summary>
    public string CustomPages
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets or sets the index into <see cref="PagesPerSheetChoices"/>.</summary>
    public int PagesPerSheetIndex
    {
        get;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ShowsPaper));
        }
    }

    /// <summary>Gets a value indicating whether the paper size matters: for a printer, or several pages per sheet.</summary>
    public bool ShowsPaper => PagesPerSheetIndex > 0 || Destination == PrintDestination.Printer;

    /// <summary>Gets the choices for pages per sheet.</summary>
    public IReadOnlyList<int> PagesPerSheetChoices => SheetGrid.Choices;

    /// <summary>Gets or sets the paper used when several pages share a sheet.</summary>
    public PaperSize Paper
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets or sets a value indicating whether notes, highlights and drawings print.</summary>
    public bool IncludeAnnotations
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    /// <summary>Gets a value indicating whether the settings name pages to print.</summary>
    public bool IsValid
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets a value indicating whether the preview is being built.</summary>
    public bool IsBuilding
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the sheet count, or what to fix, for the top of the settings panel.</summary>
    public string Summary
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets the sheets that will print.</summary>
    public ObservableCollection<PrintPreviewPage> Sheets { get; } = [];

    /// <summary>Gets the file that will print, once built.</summary>
    public string? PreviewPath { get; private set; }

    /// <summary>Gets the command confirming the print.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ConfirmCommand { get; }

    /// <summary>Gets the command skipping these settings and printing the whole document from the desktop's own dialog.</summary>
    public ReactiveCommand<RxVoid, RxVoid> SystemDialogCommand { get; }

    /// <summary>Gets the confirmations, which close the window.</summary>
    public IObservable<RxVoid> Confirmed => _confirmed;

    /// <summary>Describes a sheet count.</summary>
    /// <param name="sheets">The sheets.</param>
    /// <returns>The description.</returns>
    public static string DescribeSheets(int sheets) => sheets == 1 ? "1 sheet" : string.Create(CultureInfo.CurrentCulture, $"{sheets} sheets");

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscriptions.Dispose();
        _confirmed.Dispose();
        ClosePreview();
    }

    /// <summary>Hands over the file that will print: the preview closes and the caller deletes the file when done.</summary>
    /// <returns>The file, or <see langword="null"/> when none is ready.</returns>
    public string? TakePrintFile()
    {
        var path = PreviewPath;
        PreviewPath = null;
        ClosePreview();
        return path;
    }

    /// <summary>Writes the chosen pages to a new temporary file.</summary>
    /// <param name="exporter">The document.</param>
    /// <param name="pages">The pages.</param>
    /// <param name="layout">The sheet layout.</param>
    /// <returns>The file, or <see langword="null"/> on failure.</returns>
    private static string? Build(IPageExporter exporter, int[] pages, SheetLayout layout)
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-print-{Guid.NewGuid():N}.pdf");
        try
        {
            bool written;
            using (var stream = File.Create(path))
            {
                written = exporter.ExportPages(pages, layout, stream);
            }

            if (written)
            {
                return path;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        Delete(path);
        return null;
    }

    /// <summary>Deletes a file, ignoring failures.</summary>
    /// <param name="path">The file.</param>
    private static void Delete(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Lists the printers off the UI thread and chooses the default one, when there are any.</summary>
    /// <returns>A task.</returns>
    private async Task LoadPrintersAsync()
    {
        var printer = _services.Platform.Printer;
        var printers = await Task.Run(printer.GetPrinters).ConfigureAwait(true);
        for (var i = 0; i < printers.Count; i++)
        {
            Targets.Insert(i, new(PrintDestination.Printer, printers[i].Name, printers[i].DisplayName));
        }

        if (printers.Count > 0 && SelectedTarget == SystemDialogTarget)
        {
            SelectedTarget = Targets[0];
        }
        else if (printers.Count == 0 && !printer.IsAvailable)
        {
            SelectedTarget = SaveAsPdfTarget;
        }
    }

    /// <summary>Rebuilds the preview after a setting changed.</summary>
    /// <param name="change">Unused.</param>
    private void OnSettingsChanged(RxVoid change) => _ = RebuildAsync();

    /// <summary>Chooses the pages from the settings.</summary>
    /// <returns><see langword="true"/> when at least one page is chosen.</returns>
    private bool ChoosePages()
    {
        _chosen.Clear();
        var count = _tab.Source.PageCount;
        switch (PageChoice)
        {
            case PrintPageChoice.Current:
            {
                _chosen.Add(Math.Clamp(_tab.CurrentPageIndex, 0, Math.Max(0, count - 1)));
                return count > 0;
            }

            case PrintPageChoice.Custom:
            {
                return PageRanges.TryParse(CustomPages, count, _chosen);
            }

            default:
            {
                for (var i = 0; i < count; i++)
                {
                    _chosen.Add(i);
                }

                return count > 0;
            }
        }
    }

    /// <summary>Builds the file that will print off the UI thread, then shows its sheets.</summary>
    /// <returns>A task.</returns>
    private async Task RebuildAsync()
    {
        var version = ++_version;
        if (_tab.TryGetDocument() is not IPageExporter exporter || !ChoosePages())
        {
            IsValid = false;
            Summary = PageChoice == PrintPageChoice.Custom ? "Type pages like 1-3, 7" : "Nothing to print";
            return;
        }

        IsBuilding = true;
        var pages = _chosen.ToArray();
        var layout = new SheetLayout(SheetGrid.Choices[Math.Clamp(PagesPerSheetIndex, 0, SheetGrid.Choices.Count - 1)], Paper, IncludeAnnotations);
        var path = await Task.Run(() => Build(exporter, pages, layout)).ConfigureAwait(true);
        if (version != _version)
        {
            Delete(path);
            return;
        }

        IsBuilding = false;
        ClosePreview();
        if (path is null)
        {
            IsValid = false;
            Summary = "Could not prepare the pages";
            return;
        }

        PreviewPath = path;
        _preview = _services.Engine.Open(path, null);
        var sizes = _preview.GetPageSizes();
        for (var i = 0; i < sizes.Length; i++)
        {
            Sheets.Add(new(_preview, i, sizes[i], string.Create(CultureInfo.CurrentCulture, $"{i + 1} of {sizes.Length}")));
        }

        Summary = DescribeSheets(sizes.Length);
        IsValid = true;
    }

    /// <summary>Closes and deletes the current preview.</summary>
    private void ClosePreview()
    {
        Sheets.Clear();
        _preview?.Dispose();
        _preview = null;
        Delete(PreviewPath);
        PreviewPath = null;
    }
}
