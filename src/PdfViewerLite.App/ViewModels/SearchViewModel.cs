// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Search;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.ViewModels;

/// <summary>Find-in-document state: the query, incremental results and the current hit.</summary>
[DebuggerDisplay("{Query}: {Results.Count}")]
public sealed class SearchViewModel : ReactiveObject, IDisposable
{
    /// <summary>The number of characters of context shown on each side of a hit.</summary>
    private const int ContextLength = 32;

    /// <summary>How long typing must pause before a search starts.</summary>
    private static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The query subscription.</summary>
    private readonly IDisposable _querySubscription;

    /// <summary>Hits grouped by page, for highlighting.</summary>
    private readonly Dictionary<int, List<SearchHit>> _hitsByPage = [];

    /// <summary>Emits when highlights change.</summary>
    private readonly Signal<RxVoid> _highlightChanges = new();

    /// <summary>Cancels the running search.</summary>
    private CancellationTokenSource? _search;

    /// <summary>Initializes a new instance of the <see cref="SearchViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public SearchViewModel(DocumentTabViewModel owner)
    {
        _owner = owner;
        NextCommand = ReactiveCommand.Create(Next);
        PreviousCommand = ReactiveCommand.Create(Previous);
        CloseCommand = ReactiveCommand.Create(Close);
        _querySubscription = this.WhenAnyValue(static x => x.Query, static x => x.MatchCase, static x => x.WholeWord)
            .Throttle(TypingDelay, RxSchedulers.MainThreadScheduler)
            .SubscribeSafe(_ => StartSearch(), OnSearchError);
    }

    /// <summary>Gets notifications that the highlights changed, so the canvas can repaint.</summary>
    public IObservable<RxVoid> HighlightChanges => _highlightChanges;

    /// <summary>Gets or sets the query.</summary>
    public string Query
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets or sets a value indicating whether letter case must match.</summary>
    public bool MatchCase
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets or sets a value indicating whether only whole words match.</summary>
    public bool WholeWord
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets or sets a value indicating whether the search bar is open.</summary>
    public bool IsOpen
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets a value indicating whether a search is running.</summary>
    public bool IsSearching
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the status text, for example "3 of 12".</summary>
    public string Status
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets the index of the current hit, or -1.</summary>
    public int CurrentIndex
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = -1;

    /// <summary>Gets or sets the selected result in the sidebar list.</summary>
    public SearchResultItemViewModel? SelectedResult
    {
        get;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref field, value);
            if (value is not null && value.Index != CurrentIndex)
            {
                MoveTo(value.Index);
            }
        }
    }

    /// <summary>Gets the results in search order.</summary>
    public ObservableCollection<SearchResultItemViewModel> Results { get; } = [];

    /// <summary>Gets the current hit, if any.</summary>
    public SearchHit? CurrentHit => CurrentIndex >= 0 && CurrentIndex < Results.Count ? Results[CurrentIndex].Hit : null;

    /// <summary>Gets the command that moves to the next hit.</summary>
    public ReactiveCommand<RxVoid, RxVoid> NextCommand { get; }

    /// <summary>Gets the command that moves to the previous hit.</summary>
    public ReactiveCommand<RxVoid, RxVoid> PreviousCommand { get; }

    /// <summary>Gets the command that closes the search bar.</summary>
    public ReactiveCommand<RxVoid, RxVoid> CloseCommand { get; }

    /// <summary>Gets the hits on a page.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <returns>The hits, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<SearchHit>? GetHits(int pageIndex) => _hitsByPage.GetValueOrDefault(pageIndex);

    /// <summary>Opens the search bar.</summary>
    public void Open()
    {
        IsOpen = true;
        _owner.SidebarMode = SidebarMode.Search;
    }

    /// <summary>Closes the search bar and clears highlights.</summary>
    public void Close()
    {
        IsOpen = false;
        Query = string.Empty;
        Cancel();
        Clear();
        if (_owner.SidebarMode == SidebarMode.Search)
        {
            _owner.SidebarMode = SidebarMode.Thumbnails;
        }
    }

    /// <summary>Moves to the next hit.</summary>
    public void Next()
    {
        if (Results.Count > 0)
        {
            MoveTo((CurrentIndex + 1) % Results.Count);
        }
    }

    /// <summary>Moves to the previous hit.</summary>
    public void Previous()
    {
        if (Results.Count > 0)
        {
            MoveTo(CurrentIndex <= 0 ? Results.Count - 1 : CurrentIndex - 1);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _querySubscription.Dispose();
        Cancel();
        NextCommand.Dispose();
        PreviousCommand.Dispose();
        CloseCommand.Dispose();
        _highlightChanges.OnCompleted();
        _highlightChanges.Dispose();
    }

    /// <summary>Restarts the search after the document reloads.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Refresh() => StartSearch();

    /// <summary>Reports a failure in the query pipeline.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnSearchError(Exception error) => Debug.WriteLine(error);

    /// <summary>Gets the text around a hit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="match">The match.</param>
    /// <returns>The context.</returns>
    private static string GetContext(IDocument document, TextMatch match)
    {
        var start = Math.Max(0, match.Start - ContextLength);
        var end = Math.Min(document.GetCharacterCount(match.PageIndex), match.Start + match.Length + ContextLength);
        var text = document.GetText(match.PageIndex, start, end - start);
        return text.ReplaceLineEndings(" ").Trim();
    }

    /// <summary>Selects a hit and scrolls to it.</summary>
    /// <param name="index">The hit index.</param>
    private void MoveTo(int index)
    {
        CurrentIndex = index;
        var item = Results[index];
        SelectedResult = item;
        UpdateStatus();
        var bounds = item.Hit.Bounds;
        _owner.NavigateTo(new(item.PageIndex, bounds.Length > 0 ? bounds[0] : null, 0));
        _highlightChanges.OnNext(RxVoid.Default);
    }

    /// <summary>Cancels the running search.</summary>
    private void Cancel()
    {
        _search?.Cancel();
        _search?.Dispose();
        _search = null;
        IsSearching = false;
    }

    /// <summary>Clears results.</summary>
    private void Clear()
    {
        Results.Clear();
        _hitsByPage.Clear();
        CurrentIndex = -1;
        SelectedResult = null;
        Status = string.Empty;
        _highlightChanges.OnNext(RxVoid.Default);
    }

    /// <summary>Starts a new search for the current query.</summary>
    private void StartSearch()
    {
        Cancel();
        Clear();
        var document = _owner.TryGetDocument();
        if (string.IsNullOrWhiteSpace(Query) || document is null)
        {
            return;
        }

        var options = (MatchCase ? SearchOptions.MatchCase : SearchOptions.None) | (WholeWord ? SearchOptions.WholeWord : SearchOptions.None);
        _search = new();
        _ = RunSearchAsync(document, Query, options, _owner.CurrentPageIndex, _search.Token);
    }

    /// <summary>Runs a search, adding results on the UI thread as pages complete.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <param name="startPage">The page to start from.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>A task.</returns>
    private async Task RunSearchAsync(IDocument document, string query, SearchOptions options, int startPage, CancellationToken cancellationToken)
    {
        IsSearching = true;
        Status = "Searching…";
        try
        {
            await foreach (var page in DocumentSearch.SearchAsync(document, query, options, Math.Max(0, startPage), cancellationToken))
            {
                var list = new List<SearchHit>(page.Hits.Count);
                foreach (var hit in page.Hits)
                {
                    list.Add(hit);
                    Results.Add(new(Results.Count, page.PageIndex, hit, GetContext(document, hit.Match)));
                }

                _hitsByPage[page.PageIndex] = list;
                if (CurrentIndex < 0)
                {
                    MoveTo(0);
                }

                UpdateStatus();
                _highlightChanges.OnNext(RxVoid.Default);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        IsSearching = false;
        UpdateStatus();
    }

    /// <summary>Updates the status text.</summary>
    private void UpdateStatus() => Status = Results.Count switch
    {
        0 when IsSearching => "Searching…",
        0 => "No results",
        _ => $"{CurrentIndex + 1} of {Results.Count}{(IsSearching ? "+" : string.Empty)}",
    };
}
