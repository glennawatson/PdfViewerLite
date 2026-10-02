// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using PdfViewerLite.Core.Platform;

namespace PdfViewerLite.App.Theming;

/// <summary>
/// Applies the desktop colour scheme (for example KDE's from <c>kdeglobals</c>) to the application's resources and the
/// Fluent theme, so the window content matches Breeze while KWin draws the window frame.
/// </summary>
internal static class DesktopThemeApplier
{
    /// <summary>Points to device independent pixels.</summary>
    private const double PixelsPerPoint = 96.0 / 72.0;

    /// <summary>The alpha of border lines.</summary>
    private const byte BorderAlpha = 0x33;

    /// <summary>The alpha of the tab hover highlight.</summary>
    private const byte HoverAlpha = 0x22;

    /// <summary>How much darker the tab bar is than the tool bar.</summary>
    private const double TabBarShade = 0.04;

    /// <summary>How much darker the page area is than the window, for light schemes.</summary>
    private const double LightCanvasShade = 0.12;

    /// <summary>How much darker the page area is than the window, for dark schemes.</summary>
    private const double DarkCanvasShade = 0.35;

    /// <summary>The resource keys this class sets.</summary>
    private static readonly string[] ResourceKeys =
    [
        "AppWindowBackground", "AppHeaderBackground", "AppViewBackground", "AppCanvasBackground", "AppForeground",
        "AppSecondaryForeground", "AppAccentBrush", "AppBorderBrush", "AppTabHoverBackground",
    ];

    /// <summary>Applies a palette, or restores the built in Breeze look when <paramref name="palette"/> is null.</summary>
    /// <param name="application">The application.</param>
    /// <param name="palette">The palette.</param>
    internal static void Apply(Application application, DesktopPalette? palette)
    {
        ArgumentNullException.ThrowIfNull(application);
        var resources = application.Resources;
        if (palette is null)
        {
            foreach (var key in ResourceKeys)
            {
                _ = resources.Remove(key);
            }

            application.RequestedThemeVariant = ThemeVariant.Default;
            return;
        }

        var variant = palette.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
        application.RequestedThemeVariant = variant;
        var window = ToColor(palette.WindowBackground);
        var foreground = ToColor(palette.WindowForeground);
        var accent = ToColor(palette.Accent);
        resources["AppWindowBackground"] = new SolidColorBrush(Shade(window, TabBarShade));
        resources["AppHeaderBackground"] = new SolidColorBrush(window);
        resources["AppViewBackground"] = new SolidColorBrush(ToColor(palette.ViewBackground));
        resources["AppCanvasBackground"] = new SolidColorBrush(Shade(window, palette.IsDark ? DarkCanvasShade : LightCanvasShade));
        resources["AppForeground"] = new SolidColorBrush(foreground);
        resources["AppSecondaryForeground"] = new SolidColorBrush(ToColor(palette.InactiveForeground));
        resources["AppAccentBrush"] = new SolidColorBrush(accent);
        resources["AppBorderBrush"] = new SolidColorBrush(Color.FromArgb(BorderAlpha, foreground.R, foreground.G, foreground.B));
        resources["AppTabHoverBackground"] = new SolidColorBrush(Color.FromArgb(HoverAlpha, accent.R, accent.G, accent.B));

        foreach (var style in application.Styles)
        {
            if (style is FluentTheme fluent)
            {
                fluent.Palettes[variant] = new() { Accent = accent, RegionColor = window, BaseHigh = foreground, AltHigh = ToColor(palette.ViewBackground) };
            }
        }
    }

    /// <summary>Applies the desktop font to a window.</summary>
    /// <param name="window">The window.</param>
    /// <param name="palette">The palette.</param>
    internal static void ApplyFont(Window window, DesktopPalette? palette)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (palette?.FontFamily is { Length: > 0 } family)
        {
            window.FontFamily = new($"{family}, Noto Sans, sans-serif");
        }

        if (palette?.FontSizePoints is { } points)
        {
            window.FontSize = Math.Round(points * PixelsPerPoint);
        }
    }

    /// <summary>Converts 0xAARRGGBB to a colour.</summary>
    /// <param name="argb">The packed 0xAARRGGBB value.</param>
    /// <returns>The colour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Color ToColor(uint argb) => Color.FromUInt32(argb);

    /// <summary>Darkens a colour.</summary>
    /// <param name="color">The colour.</param>
    /// <param name="amount">The fraction to darken by.</param>
    /// <returns>The darker colour.</returns>
    private static Color Shade(Color color, double amount)
    {
        var factor = 1 - amount;
        return Color.FromRgb((byte)(color.R * factor), (byte)(color.G * factor), (byte)(color.B * factor));
    }
}
