// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.Core.Settings;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// Focus Mode for a tab: the document's text in reading order, reflowed into one calm column with the size, spacing,
/// width, colour and typeface the person chooses. Switching between it and the pages keeps the place, Read Aloud is
/// followed and marked, and Read from Here starts reading at a paragraph.
/// </summary>
[DebuggerDisplay("On={IsOn}")]
public sealed class FocusModeViewModel : ReactiveObject, IDisposable
{
    /// <summary>The smallest text size.</summary>
    private const double MinFontSize = 12;

    /// <summary>The largest text size.</summary>
    private const double MaxFontSize = 32;

    /// <summary>How much Larger and Smaller change the text size.</summary>
    private const double FontStep = 1;

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The application services.</summary>
    private readonly AppServices _services;

    /// <summary>Requests to bring a place into view.</summary>
    private readonly Signal<FocusScrollRequest> _scrollRequests = new();

    /// <summary>The subscriptions.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>The page shown at the top of the view.</summary>
    private int _topPage;

    /// <summary>How far down the top page the view is.</summary>
    private double _topFraction;

    /// <summary>The page last marked as being read.</summary>
    private int _markedPage = -1;

    /// <summary>Initializes a new instance of the <see cref="FocusModeViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    /// <param name="services">The application services.</param>
    public FocusModeViewModel(DocumentTabViewModel owner, AppServices services)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(services);
        _owner = owner;
        _services = services;
        ToggleCommand = ReactiveCommand.Create(() => IsOn = !IsOn);
        LargerCommand = ReactiveCommand.Create(() => FontSize = Math.Min(MaxFontSize, FontSize + FontStep));
        SmallerCommand = ReactiveCommand.Create(() => FontSize = Math.Max(MinFontSize, FontSize - FontStep));
        _subscriptions.Add(owner.ReadAloud.MarksChanged.SubscribeSafe(_ => MarkSpoken(), static error => Trace.TraceError(error.ToString())));
    }

    /// <summary>Gets the page colour choices.</summary>
    public static IReadOnlyList<string> PageColourOptions { get; } = ["Match the pages", "Soft paper", "Sage", "Calm night", "White"];

    /// <summary>Gets the typeface choices.</summary>
    public static IReadOnlyList<string> FontOptions { get; } = ["Sans serif", "Serif"];

    /// <summary>Gets or sets a value indicating whether Focus Mode is shown instead of the pages.</summary>
    public bool IsOn
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            _ = this.RaiseAndSetIfChanged(ref field, value);
            if (value)
            {
                Open();
            }
            else
            {
                _owner.NavigateTo(new(_topPage, null, _topFraction));
            }
        }
    }

    /// <summary>Gets the pages, created when Focus Mode first opens.</summary>
    public IReadOnlyList<FocusPageViewModel> Pages
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    /// <summary>Gets the requests to bring a place into view.</summary>
    public IObservable<FocusScrollRequest> ScrollRequests => _scrollRequests;

    /// <summary>Gets or sets the text size in points.</summary>
    public double FontSize
    {
        get => _services.Settings.FocusFontSize;
        set => Update(() => _services.Settings.FocusFontSize = Math.Clamp(value, MinFontSize, MaxFontSize), nameof(FontSize));
    }

    /// <summary>Gets or sets the line spacing, as a multiple of the text size.</summary>
    public double LineSpacing
    {
        get => _services.Settings.FocusLineSpacing;
        set => Update(() => _services.Settings.FocusLineSpacing = value, nameof(LineSpacing));
    }

    /// <summary>Gets or sets the space between paragraphs, as a multiple of the text size.</summary>
    public double ParagraphSpacing
    {
        get => _services.Settings.FocusParagraphSpacing;
        set => Update(() => _services.Settings.FocusParagraphSpacing = value, nameof(ParagraphSpacing));
    }

    /// <summary>Gets or sets the text column width, in characters.</summary>
    public double TextWidth
    {
        get => _services.Settings.FocusTextWidth;
        set => Update(() => _services.Settings.FocusTextWidth = (int)Math.Round(value), nameof(TextWidth));
    }

    /// <summary>Gets or sets the page colour's index in <see cref="PageColourOptions"/>.</summary>
    public int PageColour
    {
        get => (int)_services.Settings.FocusPageColour;
        set => Update(() => _services.Settings.FocusPageColour = (FocusPageColour)Math.Clamp(value, 0, PageColourOptions.Count - 1), nameof(PageColour));
    }

    /// <summary>Gets or sets the typeface's index in <see cref="FontOptions"/>.</summary>
    public int Font
    {
        get => (int)_services.Settings.FocusFont;
        set => Update(() => _services.Settings.FocusFont = (FocusFont)Math.Clamp(value, 0, FontOptions.Count - 1), nameof(Font));
    }

    /// <summary>Gets or sets a value indicating whether text away from what is being read aloud is dimmed.</summary>
    public bool FocusBand
    {
        get => _services.Settings.FocusBand;
        set
        {
            Update(() => _services.Settings.FocusBand = value, nameof(FocusBand));
            MarkSpoken();
        }
    }

    /// <summary>Gets or sets a value indicating whether the word being read aloud is marked as well as the sentence.</summary>
    public bool WordHighlight
    {
        get => _services.Settings.ReadAloudHighlight == ReadAloudHighlight.SentenceAndWord;
        set => Update(() => _services.Settings.ReadAloudHighlight = value ? ReadAloudHighlight.SentenceAndWord : ReadAloudHighlight.Sentence, nameof(WordHighlight));
    }

    /// <summary>Gets the command that switches Focus Mode on or off (Ctrl+4).</summary>
    public ReactiveCommand<RxVoid, bool> ToggleCommand { get; }

    /// <summary>Gets the command that makes the text larger.</summary>
    public ReactiveCommand<RxVoid, double> LargerCommand { get; }

    /// <summary>Gets the command that makes the text smaller.</summary>
    public ReactiveCommand<RxVoid, double> SmallerCommand { get; }

    /// <summary>Records the place at the top of the view, so switching back to the pages keeps it.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="fraction">How far down the page.</param>
    public void ReportTop(int pageIndex, double fraction)
    {
        _topPage = Math.Max(0, pageIndex);
        _topFraction = Math.Clamp(fraction, 0, 1);
    }

    /// <summary>Starts reading aloud at a block.</summary>
    /// <param name="block">The block.</param>
    public void ReadFromHere(FocusBlockViewModel block)
    {
        ArgumentNullException.ThrowIfNull(block);
        _owner.ReadAloud.StartAt(block.PageIndex, Math.Max(0, block.FirstCharacter));
    }

    /// <summary>Gets the share of a page's height a box's top is at.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="top">The box's top in points.</param>
    /// <returns>From 0 to 1.</returns>
    public double FractionOf(int pageIndex, float top)
    {
        var sizes = _owner.Source.PageSizes;
        return pageIndex >= 0 && pageIndex < sizes.Length && sizes[pageIndex].Height > 0 ? Math.Clamp(top / sizes[pageIndex].Height, 0, 1) : 0;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscriptions.Dispose();
        _scrollRequests.Dispose();
    }

    /// <summary>Creates the pages if needed and brings the place being viewed into view.</summary>
    private void Open()
    {
        if (Pages.Count != _owner.PageCount)
        {
            var pages = new FocusPageViewModel[_owner.PageCount];
            for (var i = 0; i < pages.Length; i++)
            {
                pages[i] = new(i, $"Page {i + 1}", _owner.GetReadingDocument);
            }

            Pages = pages;
        }

        var position = _owner.Position;
        ReportTop(position.PageIndex, position.OffsetFraction);
        _scrollRequests.OnNext(new(position.PageIndex, position.OffsetFraction));
    }

    /// <summary>Marks what is being read aloud, and follows it to a new page.</summary>
    private void MarkSpoken()
    {
        var reader = _owner.ReadAloud;
        if (_markedPage >= 0 && _markedPage < Pages.Count && _markedPage != reader.SpokenPage)
        {
            Pages[_markedPage].Mark(TextRange.None, TextRange.None, false);
        }

        _markedPage = reader.SpokenPage;
        if (_markedPage < 0 || _markedPage >= Pages.Count)
        {
            return;
        }

        var page = Pages[_markedPage];
        page.Mark(reader.SpokenRange, reader.SpokenWord, FocusBand);
        if (IsOn && reader.IsPlaying && _markedPage != _topPage)
        {
            _scrollRequests.OnNext(new(_markedPage, 0));
        }
    }

    /// <summary>Applies a setting and saves it.</summary>
    /// <param name="change">The change.</param>
    /// <param name="propertyName">The property that changed.</param>
    private void Update(Action change, string propertyName)
    {
        change();
        this.RaisePropertyChanged(propertyName);
        _services.SaveSettings();
    }
}
