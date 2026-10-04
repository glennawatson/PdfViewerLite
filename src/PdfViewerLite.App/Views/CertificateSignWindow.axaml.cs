// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>Asks for a certificate and its password to sign with. Closes with <see langword="true"/> to sign.</summary>
[DebuggerDisplay("{Title}")]
public sealed partial class CertificateSignWindow : Window, IViewFor<CertificateSignViewModel>
{
    /// <summary>Defines the <see cref="ViewModel"/> property.</summary>
    public static readonly StyledProperty<CertificateSignViewModel?> ViewModelProperty = AvaloniaProperty.Register<CertificateSignWindow, CertificateSignViewModel?>(nameof(ViewModel));

    /// <summary>The bindings made while open.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="CertificateSignWindow"/> class.</summary>
    public CertificateSignWindow() => InitializeComponent();

    /// <inheritdoc/>
    public CertificateSignViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = value as CertificateSignViewModel;
    }

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _bindings =
        [
            this.Bind(ViewModel, static vm => vm.CertificatePath, static v => v.CertificateBox.Text, static text => text, static text => text ?? string.Empty),
            this.Bind(ViewModel, static vm => vm.Password, static v => v.PasswordBox.Text, static text => text, static text => text ?? string.Empty),
            this.Bind(ViewModel, static vm => vm.Reason, static v => v.ReasonBox.Text, static text => text, static text => text ?? string.Empty),
            this.Bind(ViewModel, static vm => vm.Location, static v => v.LocationBox.Text, static text => text, static text => text ?? string.Empty),
            this.OneWayBind(ViewModel, static vm => vm.Error, static v => v.ErrorText.Text),
            this.OneWayBind(ViewModel, static vm => vm.Error, static v => v.ErrorText.IsVisible, static error => error is not null),
            this.BindCommand(ViewModel, static vm => vm.BrowseCommand, static v => v.BrowseButton),
            this.BindCommand(ViewModel, static vm => vm.SignCommand, static v => v.SignButton),
            this.BindInteraction(ViewModel, static vm => vm.BrowseInteraction, BrowseAsync),
            this.WhenAnyObservable(static v => v.ViewModel!.Confirmed).SubscribeSafe(_ => Close(true), OnError),
            CancelButton.GetObservable(Button.ClickEvent, RoutingStrategies.Bubble).SubscribeSafe(_ => Close(false), OnError),
        ];
        _ = (string.IsNullOrEmpty(ViewModel?.CertificatePath) ? CertificateBox : PasswordBox).Focus();
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _bindings?.Dispose();
        _bindings = null;
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Asks for a certificate file through the desktop's open dialog.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task BrowseAsync(IInteractionContext<RxVoid, string?> context)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "Choose a Certificate",
            AllowMultiple = false,
            FileTypeFilter = [new("Certificates (.p12, .pfx)") { Patterns = ["*.p12", "*.pfx"] }],
        });
        context.SetOutput(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }
}
