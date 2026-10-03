// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Printing;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>The print window: a preview of every sheet and the print settings. Closes with <see langword="true"/> to print.</summary>
[DebuggerDisplay("{Title}")]
public sealed partial class PrintPreviewWindow : Window, IViewFor<PrintPreviewViewModel>
{
    /// <summary>Defines the <see cref="ViewModel"/> property.</summary>
    public static readonly StyledProperty<PrintPreviewViewModel?> ViewModelProperty = AvaloniaProperty.Register<PrintPreviewWindow, PrintPreviewViewModel?>(nameof(ViewModel));

    /// <summary>The bindings made while open.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="PrintPreviewWindow"/> class.</summary>
    public PrintPreviewWindow()
    {
        InitializeComponent();
        SheetList.ItemTemplate = new FuncDataTemplate<PrintPreviewPage>(static (_, _) => new PrintPreviewPageView());
        DestinationBox.ItemTemplate = new FuncDataTemplate<PrintTarget>(static (target, _) => new TextBlock { Text = target?.Label });
        foreach (var choice in SheetGrid.Choices)
        {
            _ = PagesPerSheetBox.Items.Add(choice.ToString(CultureInfo.CurrentCulture));
        }
    }

    /// <inheritdoc/>
    public PrintPreviewViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = value as PrintPreviewViewModel;
    }

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _bindings =
        [
            this.OneWayBind(ViewModel, static vm => vm.FileName, static v => v.Title, static name => $"Print {name}"),
            this.OneWayBind(ViewModel, static vm => vm.Summary, static v => v.SummaryText.Text),
            this.OneWayBind(ViewModel, static vm => vm.Sheets, static v => v.SheetList.ItemsSource),
            this.OneWayBind(ViewModel, static vm => vm.Targets, static v => v.DestinationBox.ItemsSource),
            this.Bind(ViewModel, static vm => vm.SelectedTarget, static v => v.DestinationBox.SelectedItem, static target => target, static item => item as PrintTarget),
            this.Bind(ViewModel, static vm => vm.Copies, static v => v.CopiesBox.Value, static copies => (decimal?)copies, static value => (int)(value ?? 1)),
            this.Bind(ViewModel, static vm => vm.Colour, static v => v.ColourBox.SelectedIndex, static colour => colour ? 0 : 1, static index => index != 1),
            this.Bind(ViewModel, static vm => vm.TwoSided, static v => v.TwoSidedBox.IsChecked, static on => on, static on => on == true),
            this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.CopiesRow.IsVisible, static d => d == PrintDestination.Printer),
            this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.ColourRow.IsVisible, static d => d == PrintDestination.Printer),
            this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.SidesRow.IsVisible, static d => d == PrintDestination.Printer),
            this.Bind(ViewModel, static vm => vm.PageChoice, static v => v.PagesBox.SelectedIndex, static c => (int)c, static i => (PrintPageChoice)Math.Max(0, i)),
            this.Bind(ViewModel, static vm => vm.CustomPages, static v => v.CustomPagesBox.Text, static text => text, static text => text ?? string.Empty),
            this.OneWayBind(ViewModel, static vm => vm.PageChoice, static v => v.CustomPagesRow.IsVisible, static c => c == PrintPageChoice.Custom),
            this.Bind(ViewModel, static vm => vm.PagesPerSheetIndex, static v => v.PagesPerSheetBox.SelectedIndex, static i => i, static i => Math.Max(0, i)),
            this.OneWayBind(ViewModel, static vm => vm.ShowsPaper, static v => v.PaperRow.IsVisible),
            this.Bind(ViewModel, static vm => vm.Paper, static v => v.PaperBox.SelectedIndex, static p => (int)p, static i => (PaperSize)Math.Max(0, i)),
            this.Bind(ViewModel, static vm => vm.IncludeAnnotations, static v => v.AnnotationsBox.IsChecked, static on => on, static on => on == true),
            this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.PrinterHint.IsVisible, static d => d == PrintDestination.SystemDialog),
            this.OneWayBind(ViewModel, static vm => vm.Destination, static v => v.PrintButton.Content, ButtonText),
            this.BindCommand(ViewModel, static vm => vm.ConfirmCommand, static v => v.PrintButton),
            this.BindCommand(ViewModel, static vm => vm.SystemDialogCommand, static v => v.SystemDialogButton),
            this.WhenAnyObservable(static v => v.ViewModel!.Confirmed).SubscribeSafe(_ => Close(true), OnError),
            CancelButton.GetObservable(Button.ClickEvent, RoutingStrategies.Bubble).SubscribeSafe(_ => Close(false), OnError),
        ];
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Key == Avalonia.Input.Key.P && e.KeyModifiers == (Avalonia.Input.KeyModifiers.Control | Avalonia.Input.KeyModifiers.Shift) && ViewModel is { } preview)
        {
            _ = preview.SystemDialogCommand.Execute().Subscribe();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _bindings?.Dispose();
        _bindings = null;
    }

    /// <summary>Gets the main button's text for a destination: Print sends at once, the others open a dialog next.</summary>
    /// <param name="destination">The destination.</param>
    /// <returns>The text.</returns>
    private static string ButtonText(PrintDestination destination) => destination switch
    {
        PrintDestination.Printer => "Print",
        PrintDestination.SaveAsPdf => "Save…",
        _ => "Print…",
    };

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());
}
