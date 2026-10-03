// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Views;

/// <summary>Shows a document's properties. The properties do not change while it is open, so it fills itself once.</summary>
[DebuggerDisplay("{Title}")]
public sealed partial class PropertiesWindow : Window, IViewFor<PropertiesViewModel>
{
    /// <summary>Defines the <see cref="ViewModel"/> property.</summary>
    public static readonly StyledProperty<PropertiesViewModel?> ViewModelProperty = AvaloniaProperty.Register<PropertiesWindow, PropertiesViewModel?>(nameof(ViewModel));

    /// <summary>The width of the name column.</summary>
    private const double NameColumnWidth = 120;

    /// <summary>The space between the name and the value.</summary>
    private const double ColumnGap = 12;

    /// <summary>The vertical space around each row.</summary>
    private const double RowGap = 3;

    /// <summary>The close button subscription.</summary>
    private readonly IDisposable _close;

    /// <summary>Initializes a new instance of the <see cref="PropertiesWindow"/> class.</summary>
    public PropertiesWindow()
    {
        InitializeComponent();
        EntryList.ItemTemplate = new FuncDataTemplate<PropertyEntry>(static (entry, _) => CreateRow(entry));
        _close = CloseButton.GetObservable(Button.ClickEvent, RoutingStrategies.Bubble).SubscribeSafe(_ => Close(), static error => Trace.TraceError(error.ToString()));
    }

    /// <inheritdoc/>
    public PropertiesViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = value as PropertiesViewModel;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property != ViewModelProperty || ViewModel is not { } viewModel)
        {
            return;
        }

        Title = $"Properties — {viewModel.Title}";
        EntryList.ItemsSource = viewModel.Entries;
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _close.Dispose();
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
}
