// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Ocr;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// Recognises the text on a tab's scanned pages, one page at a time off the UI thread, so the pages become searchable
/// and their text can be selected and copied. Pages that already have text are skipped. Progress is shown as a steady
/// "page n of m" with a Stop button; the result waits in the tab's notice until dismissed.
/// </summary>
[DebuggerDisplay("Recognizing={IsRunning}")]
public sealed class TextRecognitionViewModel : ReactiveObject
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The application services.</summary>
    private readonly AppServices _services;

    /// <summary>Cancels the running recognition.</summary>
    private CancellationTokenSource? _cancellation;

    /// <summary>Initializes a new instance of the <see cref="TextRecognitionViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    /// <param name="services">The application services.</param>
    public TextRecognitionViewModel(DocumentTabViewModel owner, AppServices services)
    {
        _owner = owner;
        _services = services;
        var idle = this.WhenAnyValue(static vm => vm.IsRunning).Select(static running => !running);
        RecognizeCommand = ReactiveCommand.CreateFromTask(RecognizeAsync, idle);
        StopCommand = ReactiveCommand.Create(Stop, this.WhenAnyValue(static vm => vm.IsRunning));
    }

    /// <summary>Gets a value indicating whether recognition is running.</summary>
    public bool IsRunning
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the progress from 0 to 1.</summary>
    public double Progress
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the progress as words, for example "Recognising text: page 3 of 12".</summary>
    public string ProgressText
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets the command recognising every page without text.</summary>
    public ReactiveCommand<RxVoid, RxVoid> RecognizeCommand { get; }

    /// <summary>Gets the command stopping after the current page.</summary>
    public ReactiveCommand<RxVoid, RxVoid> StopCommand { get; }

    /// <summary>Describes the outcome of a run.</summary>
    /// <param name="recognized">Pages given text.</param>
    /// <param name="alreadyText">Pages that already had text.</param>
    /// <param name="pageCount">Pages in the document.</param>
    /// <param name="stopped">Whether the user stopped early.</param>
    /// <returns>The message.</returns>
    internal static string Describe(int recognized, int alreadyText, int pageCount, bool stopped)
    {
        if (alreadyText == pageCount)
        {
            return "Every page already has text, so there was nothing to recognise.";
        }

        var pages = recognized == 1 ? "1 page" : $"{recognized} pages";
        var prefix = stopped ? "Stopped. " : string.Empty;
        return recognized == 0
            ? $"{prefix}No text was found on the scanned pages."
            : $"{prefix}Text recognised on {pages}. You can now search, select and copy it. Save to keep it.";
    }

    /// <summary>Starts the engine and recognises the pages, then reports the outcome in the tab's notice.</summary>
    /// <returns>A task.</returns>
    private async Task RecognizeAsync()
    {
        if (_owner.TryGetDocument() is not { } document || document is not ITextLayerWriter writer)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsRunning = true;
        try
        {
            ProgressText = "Starting text recognition…";
            using var engine = await Task.Run(_services.CreateOcrEngine, cancellation.Token).ConfigureAwait(true);
            if (!engine.IsAvailable)
            {
                _owner.Notice = "Text recognition needs Tesseract and its language data. Install the tesseract and tesseract-data-eng packages (names vary by distribution), then try again.";
                return;
            }

            var run = await RecognizePagesAsync(document, writer, engine, cancellation.Token).ConfigureAwait(true);
            _owner.Search.Refresh();
            _owner.Notice = run.Closed
                ? "Text recognition stopped because the document was closed."
                : Describe(run.Recognized, run.AlreadyText, document.PageCount, cancellation.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            _owner.Notice = "Text recognition stopped.";
        }
        finally
        {
            _cancellation = null;
            IsRunning = false;
            Progress = 0;
            ProgressText = string.Empty;
        }
    }

    /// <summary>Recognises each page in turn off the UI thread, reporting progress and edits on the UI thread.</summary>
    /// <param name="document">The document.</param>
    /// <param name="writer">The document's text layer writer.</param>
    /// <param name="engine">The recogniser.</param>
    /// <param name="cancellation">Stops before the next page.</param>
    /// <returns>What happened.</returns>
    private async Task<RecognitionRun> RecognizePagesAsync(Core.Documents.IDocument document, ITextLayerWriter writer, IOcrEngine engine, CancellationToken cancellation)
    {
        var run = default(RecognitionRun);
        var pageCount = document.PageCount;
        List<OcrWord> words = [];
        for (var page = 0; page < pageCount && !cancellation.IsCancellationRequested; page++)
        {
            Progress = (double)page / pageCount;
            ProgressText = $"Recognising text: page {page + 1} of {pageCount}";
            var index = page;
            var result = await Task.Run(() => OcrRunner.RecognizePage(document, writer, engine, index, words), CancellationToken.None).ConfigureAwait(true);
            switch (result.Status)
            {
                case OcrPageStatus.Recognized:
                {
                    run = run with { Recognized = run.Recognized + 1 };
                    _owner.OnPageEdited(index);
                    break;
                }

                case OcrPageStatus.AlreadyHasText:
                {
                    run = run with { AlreadyText = run.AlreadyText + 1 };
                    break;
                }

                case OcrPageStatus.NotRendered:
                {
                    return run with { Closed = true };
                }

                default:
                {
                    break;
                }
            }
        }

        return run;
    }

    /// <summary>Stops after the page being recognised.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Stop() => _cancellation?.Cancel();
}
