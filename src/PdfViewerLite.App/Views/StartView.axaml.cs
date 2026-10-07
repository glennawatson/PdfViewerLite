// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
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
            disposables.Add(ItemAutomation.NameItems(RecentList));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.OpenCommand, static v => v.OpenButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RecentDocuments, static v => v.RecentList.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.RecentDocuments.Count, static v => v.RecentPanel.IsVisible, static count => count > 0));
            disposables.Add(Picks(RecentList).InvokeCommand(ViewModel, static vm => vm.OpenRecentCommand));
        });
    }

    /// <summary>Focuses the Open button.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void FocusOpenButton() => _ = OpenButton.Focus();

    /// <summary>Gets the recent documents picked with a click or Enter, never just because the selection moved.</summary>
    /// <param name="recent">The recent list; Events() needs the typed parameter because it cannot see fields the XAML name generator creates.</param>
    /// <returns>
    /// The picked documents, read from the item the event came from. The declared type matters: the binding generator
    /// cannot see the types Events() generates, so InvokeCommand must start from this method's result.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<RecentDocument> Picks(ListBox recent) =>
        Signal.Merge(
                recent.Events().Tapped.Select(static args => args.Source),
                recent.Events().KeyDown.Where(static args => args.Key == Key.Enter).Select(static args => args.Source))
            .Select(static source => (source as StyledElement)?.DataContext)
            .OfType<RecentDocument>();
}
