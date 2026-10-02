// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests the desktop backend configured by the application entry point.</summary>
public sealed class ProgramTests
{
    /// <summary>The compositor socket used by the test sessions.</summary>
    private const string WaylandDisplay = "wayland-0";

    /// <summary>Verifies that a Linux session without X11 configures native Wayland.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BuildAvaloniaApp_WaylandOnly_ConfiguresWayland()
    {
        var expected = AppBuilder.Configure<App>().UseWayland().WindowingSubsystemInitializer;
        await Assert.That<Action?>(Program.BuildAvaloniaApp(true, null, WaylandDisplay).WindowingSubsystemInitializer).IsEqualTo(expected);
    }

    /// <summary>Verifies that sessions with X11 keep the platform's default backend.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BuildAvaloniaApp_BothDisplays_KeepsDefaultBackend()
    {
        var expected = AppBuilder.Configure<App>().UsePlatformDetect().WindowingSubsystemInitializer;
        await Assert.That<Action?>(Program.BuildAvaloniaApp(true, ":0", WaylandDisplay).WindowingSubsystemInitializer).IsEqualTo(expected);
    }

    /// <summary>Verifies that Wayland environment values do not override other operating systems.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BuildAvaloniaApp_NonLinux_KeepsDefaultBackend()
    {
        var expected = AppBuilder.Configure<App>().UsePlatformDetect().WindowingSubsystemInitializer;
        await Assert.That<Action?>(Program.BuildAvaloniaApp(false, null, WaylandDisplay).WindowingSubsystemInitializer).IsEqualTo(expected);
    }
}
