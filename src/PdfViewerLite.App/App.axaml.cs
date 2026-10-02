// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PdfViewerLite.App.Services;
using PdfViewerLite.App.Theming;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Platform.Linux.DBus;

namespace PdfViewerLite.App;

/// <summary>The Avalonia application.</summary>
[DebuggerDisplay("PdfViewerLite")]
public sealed class App : Application
{
    /// <summary>The services, once started.</summary>
    private AppServices? _services;

    /// <summary>The main window, once created.</summary>
    private MainWindow? _window;

    /// <summary>Shuts down cleanly on SIGTERM (for example at logout) so the session is saved.</summary>
    private PosixSignalRegistration? _terminateRegistration;

    /// <inheritdoc/>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc/>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = AppServices.CreateDefault();
            _services = services;
            ApplyTheme();
            if (services.ThemeSource is { } themeSource)
            {
                themeSource.PaletteChanged += (_, _) => Dispatcher.UIThread.Post(ApplyTheme);
            }

            var viewModel = new MainViewModel(services);
            var window = new MainWindow { DataContext = viewModel, Width = services.Settings.WindowWidth, Height = services.Settings.WindowHeight };
            if (services.Settings.WindowMaximized)
            {
                window.WindowState = Avalonia.Controls.WindowState.Maximized;
            }

            DesktopThemeApplier.ApplyFont(window, services.ThemeSource?.Palette);
            _window = window;
            viewModel.RestoreSession();
            viewModel.Open(Program.StartupDocuments);
            if (Program.InstanceHost is { } host)
            {
                host.OpenRequested += OnOpenRequested;
            }

            window.Closing += (_, _) => RememberWindow(window, services);
            desktop.MainWindow = window;
            if (!OperatingSystem.IsWindows())
            {
                _terminateRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
                {
                    context.Cancel = true;
                    Dispatcher.UIThread.Post(static state => ((IClassicDesktopStyleApplicationLifetime)state!).Shutdown(), desktop);
                });
            }

            desktop.Exit += (_, _) =>
            {
                _terminateRegistration?.Dispose();
                viewModel.Dispose();
                services.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Records the window size for the next start.</summary>
    /// <param name="window">The window.</param>
    /// <param name="services">The services.</param>
    private static void RememberWindow(MainWindow window, AppServices services)
    {
        services.Settings.WindowMaximized = window.WindowState == Avalonia.Controls.WindowState.Maximized;
        if (window.WindowState == Avalonia.Controls.WindowState.Normal)
        {
            services.Settings.WindowWidth = window.Width;
            services.Settings.WindowHeight = window.Height;
        }

        services.SaveSettings();
    }

    /// <summary>Applies the desktop colour scheme.</summary>
    private void ApplyTheme()
    {
        var palette = _services?.ThemeSource?.Palette;
        DesktopThemeApplier.Apply(this, palette);
        if (_window is not null)
        {
            DesktopThemeApplier.ApplyFont(_window, palette);
        }
    }

    /// <summary>Queues documents forwarded by another launch, for example from Dolphin.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The request.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnOpenRequested(object? sender, OpenRequestEventArgs e) => Dispatcher.UIThread.Post(OpenForwarded, e);

    /// <summary>Opens forwarded documents and raises the window.</summary>
    /// <param name="state">The <see cref="OpenRequestEventArgs"/>.</param>
    private void OpenForwarded(object? state)
    {
        if (state is not OpenRequestEventArgs request || _window?.ViewModel is not { } viewModel)
        {
            return;
        }

        viewModel.Open(request.Uris);
        _window.BringToFront();
    }
}
