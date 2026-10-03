// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>Shows the comfort preferences; each choice applies as soon as it is made.</summary>
[DebuggerDisplay("{Title}")]
public sealed partial class PreferencesWindow : Window, IViewFor<PreferencesViewModel>
{
    /// <summary>Defines the <see cref="ViewModel"/> property.</summary>
    public static readonly StyledProperty<PreferencesViewModel?> ViewModelProperty = AvaloniaProperty.Register<PreferencesWindow, PreferencesViewModel?>(nameof(ViewModel));

    /// <summary>The bindings made while open.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="PreferencesWindow"/> class.</summary>
    public PreferencesWindow()
    {
        InitializeComponent();
        SchemeBox.ItemsSource = PreferencesViewModel.ColorSchemeOptions;
        PageToneBox.ItemsSource = PreferencesViewModel.PageToneOptions;
        ToolbarBox.ItemsSource = PreferencesViewModel.ToolbarOptions;
        FileChangeBox.ItemsSource = PreferencesViewModel.FileChangeOptions;
        MotionBox.ItemsSource = PreferencesViewModel.MotionOptions;
        CaretBox.ItemsSource = PreferencesViewModel.CaretOptions;
        FontSizeBox.ItemsSource = PreferencesViewModel.FontSizeOptions;
        SpeechEngineBox.ItemsSource = PreferencesViewModel.SpeechEngineOptions;
    }

    /// <inheritdoc/>
    public PreferencesViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = value as PreferencesViewModel;
    }

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _bindings =
        [
            this.Bind(ViewModel, static vm => vm.ColorScheme, static v => v.SchemeBox.SelectedIndex),
            this.Bind(ViewModel, static vm => vm.PageTone, static v => v.PageToneBox.SelectedIndex),
            this.Bind(ViewModel, static vm => vm.Toolbar, static v => v.ToolbarBox.SelectedIndex),
            this.Bind(ViewModel, static vm => vm.FileChange, static v => v.FileChangeBox.SelectedIndex),
            this.Bind(ViewModel, static vm => vm.Motion, static v => v.MotionBox.SelectedIndex),
            this.Bind(ViewModel, static vm => vm.Caret, static v => v.CaretBox.SelectedIndex),
            this.Bind(ViewModel, static vm => vm.FontSize, static v => v.FontSizeBox.SelectedIndex),
            this.Bind(ViewModel, static vm => vm.SpeechEngine, static v => v.SpeechEngineBox.SelectedIndex),
            this.Bind(ViewModel, static vm => vm.AzureKey, static v => v.AzureKeyBox.Text),
            this.Bind(ViewModel, static vm => vm.AzureRegion, static v => v.AzureRegionBox.Text),
            this.Bind(ViewModel, static vm => vm.TimestampServer, static v => v.TimestampServerBox.Text),
            this.OneWayBind(ViewModel, static vm => vm.UsesAzure, static v => v.AzureKeyLabel.IsVisible),
            this.OneWayBind(ViewModel, static vm => vm.UsesAzure, static v => v.AzureKeyBox.IsVisible),
            this.OneWayBind(ViewModel, static vm => vm.UsesAzure, static v => v.AzureRegionLabel.IsVisible),
            this.OneWayBind(ViewModel, static vm => vm.UsesAzure, static v => v.AzureRegionBox.IsVisible),
            this.OneWayBind(ViewModel, static vm => vm.UsesAzure, static v => v.SpeechNote.Text, DescribeSpeech),
            CloseButton.GetObservable(Button.ClickEvent, RoutingStrategies.Bubble).SubscribeSafe(_ => Close(), static error => Trace.TraceError(error.ToString())),
        ];
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _bindings?.Dispose();
        _bindings = null;
    }

    /// <summary>Explains where the chosen voice runs.</summary>
    /// <param name="azure">Whether Azure is chosen.</param>
    /// <returns>The note.</returns>
    private static string DescribeSpeech(bool azure) => azure
        ? "Each sentence read aloud is sent to your own Azure Speech resource, which bills your Azure account. The key is kept in your settings file, readable only by you."
        : "The voice runs on this computer, so nothing you read is sent anywhere. It is downloaded once, the first time you use Read Aloud.";
}
