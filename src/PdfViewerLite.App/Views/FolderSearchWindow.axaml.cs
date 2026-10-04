// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>The Search in Folder window: every PDF in a folder searched, with the results listed as they arrive.</summary>
[DebuggerDisplay("{Title}")]
public sealed partial class FolderSearchWindow : Window, IViewFor<FolderSearchViewModel>
{
    /// <summary>Defines the <see cref="ViewModel"/> property.</summary>
    public static readonly StyledProperty<FolderSearchViewModel?> ViewModelProperty = AvaloniaProperty.Register<FolderSearchWindow, FolderSearchViewModel?>(nameof(ViewModel));

    /// <summary>The bindings, while loaded.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="FolderSearchWindow"/> class.</summary>
    public FolderSearchWindow()
    {
        InitializeComponent();
        ResultList.ItemTemplate = new FuncDataTemplate<FolderSearchResultViewModel>(static (result, _) => FolderSearchResultView.Create(result));
    }

    /// <summary>Gets or sets the view model.</summary>
    public FolderSearchViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = value as FolderSearchViewModel;
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _bindings =
        [
            this.Bind(ViewModel, static vm => vm.Folder, static v => v.FolderBox.Text),
            this.Bind(ViewModel, static vm => vm.Query, static v => v.QueryBox.Text),
            this.Bind(ViewModel, static vm => vm.MatchCase, static v => v.MatchCaseBox.IsChecked, static on => on, static value => value == true),
            this.Bind(ViewModel, static vm => vm.WholeWord, static v => v.WholeWordBox.IsChecked, static on => on, static value => value == true),
            this.Bind(ViewModel, static vm => vm.IncludeSubfolders, static v => v.SubfoldersBox.IsChecked, static on => on, static value => value == true),
            this.OneWayBind(ViewModel, static vm => vm.Status, static v => v.StatusText.Text),
            this.OneWayBind(ViewModel, static vm => vm.Results, static v => v.ResultList.ItemsSource),
            this.OneWayBind(ViewModel, static vm => vm.IsSearching, static v => v.StopButton.IsEnabled),
            this.BindCommand(ViewModel, static vm => vm.ChooseFolderCommand, static v => v.ChooseFolderButton),
            this.BindCommand(ViewModel, static vm => vm.SearchCommand, static v => v.SearchButton),
            this.BindCommand(ViewModel, static vm => vm.StopCommand, static v => v.StopButton),
            this.BindInteraction(ViewModel, static vm => vm.ChooseFolderInteraction, ChooseFolderAsync),
            CloseButton.GetObservable(Button.ClickEvent, RoutingStrategies.Bubble).SubscribeSafe(_ => Close(), OnError),
            ResultList.GetObservable(DoubleTappedEvent, RoutingStrategies.Bubble).SubscribeSafe(_ => OpenSelected(), OnError),
            ResultList.GetObservable(KeyDownEvent, RoutingStrategies.Bubble).SubscribeSafe(OnResultKey, OnError),
        ];
        _ = QueryBox.Focus();
    }

    /// <inheritdoc/>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _bindings?.Dispose();
        _bindings = null;
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ViewModel?.Stop();
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Opens the selected result.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void OpenSelected() => ViewModel?.OpenCommand.Execute(ResultList.SelectedItem as FolderSearchResultViewModel).SubscribeSafe(static _ => { }, OnError);

    /// <summary>Opens the selected result when Enter is pressed in the list.</summary>
    /// <param name="e">The key press.</param>
    private void OnResultKey(KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        OpenSelected();
        e.Handled = true;
    }

    /// <summary>Asks for the folder to search.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ChooseFolderAsync(IInteractionContext<RxVoid, string?> context)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new() { Title = "Search in Folder", AllowMultiple = false });
        context.SetOutput(folders.Count > 0 ? folders[0].TryGetLocalPath() : null);
    }
}
