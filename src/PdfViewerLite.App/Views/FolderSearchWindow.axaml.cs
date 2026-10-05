// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PdfViewerLite.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.Views;

/// <summary>The Search in Folder window: every PDF in a folder searched, with the results listed as they arrive.</summary>
[DebuggerDisplay("{Title}")]
public sealed partial class FolderSearchWindow : ReactiveUI.Avalonia.ReactiveWindow<FolderSearchViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="FolderSearchWindow"/> class.</summary>
    public FolderSearchWindow()
    {
        InitializeComponent();
        ResultList.ItemTemplate = new FuncDataTemplate<FolderSearchResultViewModel>(static (result, _) => FolderSearchResultView.Create(result));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.Bind(ViewModel, static vm => vm.Folder, static v => v.FolderBox.Text));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Query, static v => v.QueryBox.Text));
            disposables.Add(this.Bind(ViewModel, static vm => vm.MatchCase, static v => v.MatchCaseBox.IsChecked, static on => on, static value => value == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.WholeWord, static v => v.WholeWordBox.IsChecked, static on => on, static value => value == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IncludeSubfolders, static v => v.SubfoldersBox.IsChecked, static on => on, static value => value == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Status, static v => v.StatusText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Results, static v => v.ResultList.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsSearching, static v => v.StopButton.IsEnabled));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ChooseFolderCommand, static v => v.ChooseFolderButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SearchCommand, static v => v.SearchButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.StopCommand, static v => v.StopButton));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.ChooseFolderInteraction, ChooseFolderAsync));

            disposables.Add(ObserveClose(CloseButton));
            disposables.Add(ObserveOpens(ResultList));

            // The window deactivates when it closes, which ends a search still running.
            disposables.Add(Scope.Create(this, static window => window.ViewModel?.Stop()));
            _ = QueryBox.Focus();
        });
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Closes the window when the button is clicked.</summary>
    /// <param name="button">The close button; Events() needs the typed parameter because it cannot see fields the XAML name generator creates.</param>
    /// <returns>The subscription.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IDisposable ObserveClose(Button button) => button.Events().Click.SubscribeSafe(_ => Close(), OnError);

    /// <summary>Opens the selected result on a double tap or Enter.</summary>
    /// <param name="results">The results list; Events() needs the typed parameter because it cannot see fields the XAML name generator creates.</param>
    /// <returns>The subscription.</returns>
    private IDisposable ObserveOpens(ListBox results)
    {
        var opens = Signal.Merge(
            results.Events().DoubleTapped.Select(static _ => RxVoid.Default),
            results.Events().KeyDown.Where(static args => args.Key == Key.Enter).Select(static args =>
            {
                args.Handled = true;
                return RxVoid.Default;
            }));
        return OpenResult(opens.Select(_ => results.SelectedItem).OfType<FolderSearchResultViewModel>());
    }

    /// <summary>Opens each chosen result.</summary>
    /// <param name="chosen">The chosen results; the binding generator needs the declared observable type here.</param>
    /// <returns>The subscription.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IDisposable OpenResult(IObservable<FolderSearchResultViewModel> chosen) => chosen.InvokeCommand(ViewModel, static vm => vm.OpenCommand);

    /// <summary>Asks for the folder to search.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ChooseFolderAsync(IInteractionContext<RxVoid, string?> context)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new() { Title = "Search in Folder", AllowMultiple = false });
        context.SetOutput(folders.Count > 0 ? folders[0].TryGetLocalPath() : null);
    }
}
