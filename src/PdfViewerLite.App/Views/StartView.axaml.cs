// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Platform;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.Views;

/// <summary>The start page: open a document, or pick a recent one.</summary>
[DebuggerDisplay("StartView")]
public sealed partial class StartView : ReactiveUI.Avalonia.ReactiveUserControl<MainViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="StartView"/> class.</summary>
    public StartView()
    {
        InitializeComponent();
        RecentList.ItemTemplate = new FuncDataTemplate<RecentDocument>(static (_, _) => new RecentDocumentView());
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.OpenCommand, static v => v.OpenButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RecentDocuments, static v => v.RecentList.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RecentDocuments.Count, static v => v.RecentPanel.IsVisible, static count => count > 0));
            disposables.Add(ObservePicks(RecentList));
        });
    }

    /// <summary>Opens a recent document on a click or Enter, never just because the selection moved.</summary>
    /// <param name="recent">The recent list; Events() needs the typed parameter because it cannot see fields the XAML name generator creates.</param>
    /// <returns>The subscription.</returns>
    private IDisposable ObservePicks(ListBox recent)
    {
        var picks = Signal.Merge(
            recent.Events().Tapped.Select(static _ => RxVoid.Default),
            recent.Events().KeyDown.Where(static args => args.Key == Key.Enter).Select(static _ => RxVoid.Default));
        return OpenRecent(picks.Select(_ => recent.SelectedItem).OfType<RecentDocument>());
    }

    /// <summary>Opens each chosen recent document.</summary>
    /// <param name="chosen">The chosen documents; the binding generator needs the declared observable type here.</param>
    /// <returns>The subscription.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IDisposable OpenRecent(IObservable<RecentDocument> chosen) => chosen.InvokeCommand(ViewModel, static vm => vm.OpenRecentCommand);
}
