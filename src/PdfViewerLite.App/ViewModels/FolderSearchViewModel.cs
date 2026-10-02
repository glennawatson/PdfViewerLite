// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Search;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// Search every PDF in a folder: results arrive file by file while the search runs off the UI thread, and opening one
/// shows its page with the words found.
/// </summary>
[DebuggerDisplay("{Query} in {Folder}: {Results.Count} results")]
public sealed class FolderSearchViewModel : ReactiveObject, IDisposable
{
    /// <summary>The most matches kept per file.</summary>
    private const int MaxMatchesPerFile = 200;

    /// <summary>The services.</summary>
    private readonly AppServices _services;

    /// <summary>Opens a result.</summary>
    private readonly Action<string, int, string> _open;

    /// <summary>Cancels the running search.</summary>
    private CancellationTokenSource? _running;

    /// <summary>Initializes a new instance of the <see cref="FolderSearchViewModel"/> class.</summary>
    /// <param name="services">The services.</param>
    /// <param name="open">Opens a file at a page with the words to show.</param>
    public FolderSearchViewModel(AppServices services, Action<string, int, string> open)
    {
        _services = services;
        _open = open;
        ChooseFolderCommand = ReactiveCommand.CreateFromTask(ChooseFolderAsync);
        SearchCommand = ReactiveCommand.CreateFromTask(SearchAsync);
        StopCommand = ReactiveCommand.Create(Stop);
        OpenCommand = ReactiveCommand.Create<FolderSearchResultViewModel?>(Open);
    }

    /// <summary>Gets the interaction that asks for a folder.</summary>
    public Interaction<RxVoid, string?> ChooseFolderInteraction { get; } = new();

    /// <summary>Gets the results, in the order the files were searched.</summary>
    public ObservableCollection<FolderSearchResultViewModel> Results { get; } = [];

    /// <summary>Gets or sets the folder searched.</summary>
    public string Folder
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets or sets the words to find.</summary>
    public string Query
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets or sets a value indicating whether case must match.</summary>
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

    /// <summary>Gets or sets a value indicating whether subfolders are searched too.</summary>
    public bool IncludeSubfolders
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    /// <summary>Gets a value indicating whether a search is running.</summary>
    public bool IsSearching
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets what the search is doing or found, such as "12 matches in 3 of 40 files".</summary>
    public string Status
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Choose a folder and the words to find.";

    /// <summary>Gets the command that asks for a folder.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ChooseFolderCommand { get; }

    /// <summary>Gets the command that starts the search.</summary>
    public ReactiveCommand<RxVoid, RxVoid> SearchCommand { get; }

    /// <summary>Gets the command that stops the search.</summary>
    public ReactiveCommand<RxVoid, RxVoid> StopCommand { get; }

    /// <summary>Gets the command that opens a result.</summary>
    public ReactiveCommand<FolderSearchResultViewModel?, RxVoid> OpenCommand { get; }

    /// <summary>Runs a search to the end, for tests and callers that wait.</summary>
    /// <returns>A task.</returns>
    public Task SearchAsync()
    {
        Stop();
        Results.Clear();
        if (string.IsNullOrWhiteSpace(Query) || !Directory.Exists(Folder))
        {
            Status = "Choose a folder and the words to find.";
            return Task.CompletedTask;
        }

        var cancellation = new CancellationTokenSource();
        _running = cancellation;
        return RunAsync(Folder, Query.Trim(), Options(), IncludeSubfolders, cancellation);
    }

    /// <summary>Stops the running search; the results so far stay.</summary>
    public void Stop()
    {
        _running?.Cancel();
        _running?.Dispose();
        _running = null;
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Stop();

    /// <summary>Counts matches in words: "1 match", "3 matches".</summary>
    /// <param name="count">The count.</param>
    /// <returns>The words.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static string Matches(int count) => string.Create(CultureInfo.CurrentCulture, $"{count} {(count == 1 ? "match" : "matches")}");

    /// <summary>Summarises the matches and the files they are in: "12 matches in 3 files".</summary>
    /// <param name="matches">The matches.</param>
    /// <param name="files">The files with matches.</param>
    /// <returns>The words.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static string Summary(int matches, int files) => string.Create(CultureInfo.CurrentCulture, $"{Matches(matches)} in {files} {(files == 1 ? "file" : "files")}");

    /// <summary>Searches the files one by one off the UI thread, adding each file's results as it finishes.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="query">The words.</param>
    /// <param name="options">The options.</param>
    /// <param name="subfolders">Whether to search subfolders.</param>
    /// <param name="cancellation">Stops the search.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(string folder, string query, SearchOptions options, bool subfolders, CancellationTokenSource cancellation)
    {
        IsSearching = true;
        var token = cancellation.Token;
        var matches = 0;
        var withMatches = 0;
        try
        {
            var files = await Task.Run(() => FolderSearch.FindFiles(folder, subfolders), token).ConfigureAwait(true);
            for (var i = 0; i < files.Count && !token.IsCancellationRequested; i++)
            {
                var path = files[i];
                var found = await Task.Run(() => FolderSearch.SearchFile(_services.Engine, path, query, options, MaxMatchesPerFile, token), token).ConfigureAwait(true);
                Add(found);
                matches += found.Matches.Count;
                withMatches += found.Matches.Count > 0 ? 1 : 0;
                Status = string.Create(CultureInfo.CurrentCulture, $"{Summary(matches, withMatches)}; searched {i + 1} of {files.Count}");
            }

            var stopped = token.IsCancellationRequested ? ", stopped" : string.Empty;
            Status = files.Count == 0 ? "There are no PDF files in this folder." : string.Create(CultureInfo.CurrentCulture, $"{Summary(matches, withMatches)} of {files.Count}{stopped}");
        }
        catch (OperationCanceledException)
        {
            Status = $"{Matches(matches)}, stopped";
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>Adds a file's results.</summary>
    /// <param name="found">What was found.</param>
    private void Add(FolderSearchFile found)
    {
        if (found.Problem is { } problem)
        {
            Results.Add(new(found.Path, -1, $"Not searched: it {problem}."));
            return;
        }

        foreach (var match in found.Matches)
        {
            Results.Add(new(found.Path, match.PageIndex, match.Snippet));
        }

        if (found.IsTruncated)
        {
            Results.Add(new(found.Path, -1, string.Create(CultureInfo.CurrentCulture, $"Only the first {MaxMatchesPerFile} matches are shown.")));
        }
    }

    /// <summary>Gets the search options.</summary>
    /// <returns>The options.</returns>
    private SearchOptions Options() =>
        (MatchCase ? SearchOptions.MatchCase : SearchOptions.None) | (WholeWord ? SearchOptions.WholeWord : SearchOptions.None);

    /// <summary>Asks for a folder.</summary>
    /// <returns>A task.</returns>
    private async Task ChooseFolderAsync()
    {
        var folder = await ChooseFolderInteraction.Handle(RxVoid.Default).ToTask().ConfigureAwait(true);
        if (!string.IsNullOrEmpty(folder))
        {
            Folder = folder;
        }
    }

    /// <summary>Opens a result at its page.</summary>
    /// <param name="result">The result.</param>
    private void Open(FolderSearchResultViewModel? result)
    {
        if (result is { IsMatch: true })
        {
            _open(result.Path, result.PageIndex, Query.Trim());
        }
    }
}
