// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using PdfViewerLite.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.ObservableEvents;

namespace PdfViewerLite.App.Views;

/// <summary>Shows a document's properties. The properties do not change while it is open, so it fills itself once.</summary>
[DebuggerDisplay("{Title}")]
public sealed partial class PropertiesWindow : ReactiveUI.Avalonia.ReactiveWindow<PropertiesViewModel>
{
    /// <summary>The width of the name column.</summary>
    private const double NameColumnWidth = 120;

    /// <summary>The space between the name and the value.</summary>
    private const double ColumnGap = 12;

    /// <summary>The vertical space around each row.</summary>
    private const double RowGap = 3;

    /// <summary>Initializes a new instance of the <see cref="PropertiesWindow"/> class.</summary>
    public PropertiesWindow()
    {
        InitializeComponent();
        EntryList.ItemTemplate = new FuncDataTemplate<PropertyEntry>(static (entry, _) => CreateRow(entry));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Title, static v => v.Title, static title => $"Properties — {title}"));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Entries, static v => v.EntryList.ItemsSource));
            disposables.Add(ObserveClose(CloseButton));
        });
    }

    /// <summary>Creates one name and value row.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The row.</returns>
    private static Grid CreateRow(PropertyEntry? entry)
    {
        var name = new TextBlock { Text = entry?.Name, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0, 0, ColumnGap, 0) };
        name.Classes.Add("secondary");
        var value = new SelectableTextBlock { Text = entry?.Value, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        Grid.SetColumn(value, 1);
        return new Grid { ColumnDefinitions = [new(NameColumnWidth, GridUnitType.Pixel), new(1, GridUnitType.Star)], Margin = new(0, RowGap), Children = { name, value } };
    }

    /// <summary>Closes the window when the button is clicked.</summary>
    /// <param name="button">The close button; Events() needs the typed parameter because it cannot see fields the XAML name generator creates.</param>
    /// <returns>The subscription.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IDisposable ObserveClose(Button button) => button.Events().Click.SubscribeSafe(_ => Close(), static error => Trace.TraceError(error.ToString()));
}
