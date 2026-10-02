// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PdfViewerLite.App.Services;
using PdfViewerLite.App.Theming;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Platform;
using PdfViewerLite.Platform.Linux.DBus;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App;

/// <summary>The Avalonia application.</summary>
[DebuggerDisplay("PdfViewerLite")]
public sealed class App : Application
{
    /// <summary>Subscriptions and resources owned for the application's lifetime.</summary>
    private readonly MultipleDisposable _lifetime = [];

    /// <summary>The main window, once created.</summary>
    private MainWindow? _window;

    /// <inheritdoc/>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Start(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Releases the services once the desktop lifetime has ended.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Release() => _lifetime.Dispose();

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Creates the services and the main window.</summary>
    /// <param name="desktop">The desktop lifetime.</param>
    private void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var services = AppServices.CreateDefault();
        var viewModel = new MainViewModel(services);
        var window = new MainWindow { DataContext = viewModel, Width = services.Settings.WindowWidth, Height = services.Settings.WindowHeight };
        if (services.Settings.WindowMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }

        _window = window;
        _lifetime.Add(services);
        _lifetime.Add(viewModel);

        // The palette's current value arrives synchronously on subscription, so the first frame is already themed.
        if (services.ThemeSource is { } themeSource)
        {
            _lifetime.Add(themeSource.Palette.SubscribeSafe(OnPalette, OnError));
        }

        if (Program.InstanceHost is { } host)
        {
            _lifetime.Add(host.OpenRequests.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(OpenForwarded, OnError));
        }

        if (!OperatingSystem.IsWindows())
        {
            // Shut down cleanly on SIGTERM (for example at logout) so the session is saved.
            _lifetime.Add(PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                Dispatcher.UIThread.Post(static state => ((IClassicDesktopStyleApplicationLifetime)state!).Shutdown(), desktop);
            }));
        }

        viewModel.RestoreSession();
        viewModel.Open(Program.StartupDocuments);
        desktop.MainWindow = window;
    }

    /// <summary>Applies a palette on the UI thread.</summary>
    /// <param name="palette">The palette.</param>
    private void OnPalette(DesktopPalette? palette)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(static state => ((App)Current!).ApplyPalette((DesktopPalette?)state), palette);
            return;
        }

        ApplyPalette(palette);
    }

    /// <summary>Applies a palette to the resources and window font.</summary>
    /// <param name="palette">The palette.</param>
    private void ApplyPalette(DesktopPalette? palette)
    {
        DesktopThemeApplier.Apply(this, palette);
        if (_window is not null)
        {
            DesktopThemeApplier.ApplyFont(_window, palette);
        }
    }

    /// <summary>Opens forwarded documents and raises the window.</summary>
    /// <param name="request">The request.</param>
    private void OpenForwarded(OpenRequest request)
    {
        if (_window?.ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.Open(request.Uris);
        _window.BringToFront();
    }
}
