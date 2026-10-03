// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Reading;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>A block of Focus Mode text: styled by its kind, with the sentence and word being read aloud marked.</summary>
[DebuggerDisplay("{ViewModel}")]
public sealed partial class FocusBlockView : UserControl, IViewFor<FocusBlockViewModel>
{
    /// <summary>Defines the <see cref="ViewModel"/> property.</summary>
    public static readonly StyledProperty<FocusBlockViewModel?> ViewModelProperty = AvaloniaProperty.Register<FocusBlockView, FocusBlockViewModel?>(nameof(ViewModel));

    /// <summary>The opacity of text dimmed by the focus band.</summary>
    private const double DimmedOpacity = 0.4;

    /// <summary>The style classes for each kind of block.</summary>
    private static readonly string[] KindClasses = ["paragraph", "heading", "item", "caption", "footnote"];

    /// <summary>The bindings made while loaded.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="FocusBlockView"/> class.</summary>
    public FocusBlockView() => InitializeComponent();

    /// <summary>Gets or sets Focus Mode, which reads aloud from a block chosen in its menu.</summary>
    public FocusModeViewModel? FocusMode { get; set; }

    /// <inheritdoc/>
    public FocusBlockViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = value as FocusBlockViewModel;
    }

    /// <summary>Gets the block's text control, for tests and place keeping.</summary>
    internal TextBlock BodyText => Body;

    /// <summary>Splits a block's text into runs for the marks: before, the sentence (with the word inside it) and after.</summary>
    /// <param name="length">The text's length.</param>
    /// <param name="spoken">The sentence, relative to the block.</param>
    /// <param name="word">The word, relative to the block.</param>
    /// <returns>Each run's start, length and whether it is the sentence or the word.</returns>
    internal static List<(int Start, int Length, bool Spoken, bool Word)> Split(int length, TextRange spoken, TextRange word)
    {
        var cuts = new SortedSet<int> { 0, length };
        foreach (var range in (ReadOnlySpan<TextRange>)[spoken, word])
        {
            if (range.IsEmpty)
            {
                continue;
            }

            _ = cuts.Add(Math.Clamp(range.Start, 0, length));
            _ = cuts.Add(Math.Clamp(range.End, 0, length));
        }

        var runs = new List<(int, int, bool, bool)>(cuts.Count);
        var previous = -1;
        foreach (var cut in cuts)
        {
            if (previous >= 0 && cut > previous)
            {
                runs.Add((previous, cut - previous, Inside(spoken, previous), Inside(word, previous)));
            }

            previous = cut;
        }

        return runs;
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ViewModel = DataContext as FocusBlockViewModel;
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _bindings =
        [
            this.WhenAnyValue(static v => v.ViewModel).SubscribeSafe(ShowBlock, OnError),
            this.WhenAnyValue(static v => v.ViewModel!.Spoken, static v => v.ViewModel!.Word, static (_, _) => RxVoid.Default).SubscribeSafe(_ => ShowMarks(), OnError),
            this.WhenAnyValue(static v => v.ViewModel!.IsDimmed).SubscribeSafe(dimmed => Opacity = dimmed ? DimmedOpacity : 1, OnError),
            this.GetObservable(ContextRequestedEvent, RoutingStrategies.Bubble).SubscribeSafe(OnContextRequested, OnError),
        ];
    }

    /// <inheritdoc/>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _bindings?.Dispose();
        _bindings = null;
    }

    /// <summary>Determines whether a position is inside a range.</summary>
    /// <param name="range">The range.</param>
    /// <param name="position">The position.</param>
    /// <returns><see langword="true"/> inside.</returns>
    private static bool Inside(TextRange range, int position) => !range.IsEmpty && position >= range.Start && position < range.End;

    /// <summary>Reports a binding failure.</summary>
    /// <param name="error">The error.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Shows a block's kind and text.</summary>
    /// <param name="block">The block.</param>
    private void ShowBlock(FocusBlockViewModel? block)
    {
        foreach (var kind in KindClasses)
        {
            _ = Body.Classes.Remove(kind);
        }

        if (block is not null)
        {
            Body.Classes.Add(KindClasses[(int)block.Kind]);
            AutomationProperties.SetName(Body, block.Text);
        }

        ShowMarks();
    }

    /// <summary>Shows the text, marking the sentence and word being read aloud.</summary>
    private void ShowMarks()
    {
        var block = ViewModel;
        Body.Inlines?.Clear();
        if (block is null)
        {
            Body.Text = null;
            return;
        }

        if (block.Spoken.IsEmpty)
        {
            Body.Text = block.Text;
            return;
        }

        Body.Text = null;
        var inlines = Body.Inlines ??= [];
        foreach (var (start, length, spoken, word) in Split(block.Text.Length, block.Spoken, block.Word))
        {
            var run = new Run(block.Text.Substring(start, length));
            if (spoken)
            {
                _ = run.Bind(TextElement.BackgroundProperty, this.GetResourceObservable("AppSpokenBrush"));
            }

            if (word)
            {
                run.TextDecorations = TextDecorations.Underline;
            }

            inlines.Add(run);
        }
    }

    /// <summary>Offers Read Aloud from Here for the block.</summary>
    /// <param name="e">The request.</param>
    private void OnContextRequested(ContextRequestedEventArgs e)
    {
        if (FocusMode is not { } focus || ViewModel is not { } block)
        {
            return;
        }

        var item = new MenuItem { Header = "_Read Aloud from Here" };
        _ = item.GetObservable(MenuItem.ClickEvent, RoutingStrategies.Bubble).SubscribeSafe(_ => focus.ReadFromHere(block), OnError);
        new ContextMenu { ItemsSource = new[] { item } }.Open(this);
        e.Handled = true;
    }
}
