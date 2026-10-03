// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Platform;
using PdfViewerLite.Platform.Linux;
using ReactiveUI.Avalonia;

namespace PdfViewerLite.App;

/// <summary>The application entry point.</summary>
public static class Program
{
    /// <summary>The argument that skips forwarding to a running instance.</summary>
    private const string NewInstanceArgument = "--new-instance";

    /// <summary>Gets the desktop integration.</summary>
    internal static IDesktopPlatform Platform { get; private set; } = new FallbackPlatform();

    /// <summary>Gets the single running window claim, when this process holds it.</summary>
    internal static ISingleInstance? InstanceHost { get; private set; }

    /// <summary>Gets the documents passed on the command line, as absolute paths or URIs.</summary>
    internal static IReadOnlyList<string> StartupDocuments { get; private set; } = [];

    /// <summary>Starts the application, or hands the documents to an already running instance.</summary>
    /// <param name="args">Files or URIs to open.</param>
    /// <returns>The exit code.</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var newInstance = Array.IndexOf(args, NewInstanceArgument) >= 0;
        StartupDocuments = NormalizeArguments(args);
        Platform = DesktopPlatforms.Detect();
        if (!newInstance)
        {
            if (Platform.TryForwardAsync(new(StartupDocuments, Platform.GetLaunchActivationToken())).GetAwaiter().GetResult())
            {
                return 0;
            }

            InstanceHost = Platform.TryClaimSingleInstanceAsync().GetAwaiter().GetResult();
        }

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            (Avalonia.Application.Current as App)?.Release();
            InstanceHost?.Dispose();
        }
    }

    /// <summary>Builds the Avalonia application. Also used by the designer.</summary>
    /// <returns>The application builder.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions { WmClass = AppIdentity.WindowClass })
            .LogToTrace()
            .UseReactiveUI(static _ => { });

    /// <summary>Turns command line arguments into absolute paths or URIs, dropping options.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The documents.</returns>
    private static List<string> NormalizeArguments(string[] args)
    {
        var documents = new List<string>(args.Length);
        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg) || arg.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            documents.Add(arg.Contains("://", StringComparison.Ordinal) ? arg : Path.GetFullPath(arg));
        }

        return documents;
    }
}
