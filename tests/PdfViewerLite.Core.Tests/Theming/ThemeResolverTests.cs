// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Platform;
using PdfViewerLite.Core.Rendering;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Core.Theming;

namespace PdfViewerLite.Core.Tests.Theming;

/// <summary>Tests for <see cref="ThemeResolver"/>.</summary>
public sealed class ThemeResolverTests
{
    /// <summary>Masks off the alpha channel.</summary>
    private const uint RgbMask = 0xFFFFFFU;

    /// <summary>A dark desktop window colour.</summary>
    private const uint DarkWindow = 0xFF202326U;

    /// <summary>A dark desktop view colour.</summary>
    private const uint DarkView = 0xFF141618U;

    /// <summary>A light desktop text colour.</summary>
    private const uint LightText = 0xFFFCFCFCU;

    /// <summary>Verifies the Calm fallback and its defaults when there is no desktop palette.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FallsBackToCalm()
    {
        var theme = ThemeResolver.Resolve(Defaults(), null);

        await Assert.That(theme.Scheme).IsEqualTo(ColorSchemes.Calm);
        await Assert.That(theme.PageTone).IsEqualTo(ColorSchemes.CalmNightTone);
        await Assert.That(theme.SteadyCaret).IsTrue();
        await Assert.That(theme.ReduceMotion).IsFalse();
        await Assert.That(theme.ShowLabels).IsTrue();
    }

    /// <summary>Verifies the desktop palette drives the scheme, tone, motion and caret.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FollowsDesktop()
    {
        const int blinkRate = 1000;
        var desktop = Desktop() with { AnimationDurationFactor = 0, CursorBlinkRateMilliseconds = blinkRate };

        var theme = ThemeResolver.Resolve(Defaults(), desktop);

        await Assert.That(theme.Scheme.Window).IsEqualTo(DarkWindow & RgbMask);
        await Assert.That(theme.Scheme.Tints).IsEqualTo(ColorSchemes.CalmTints);
        await Assert.That(theme.PageTone.Paper).IsEqualTo(DarkView & RgbMask);
        await Assert.That(theme.ReduceMotion).IsTrue();
        await Assert.That(theme.SteadyCaret).IsFalse();
    }

    /// <summary>Verifies explicit choices win over the desktop.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExplicitChoicesWin()
    {
        var settings = new AppSettings
        {
            ColorScheme = ColorSchemeChoice.Light,
            PageTone = PageToneChoice.White,
            Motion = MotionPreference.Reduced,
            Caret = CaretPreference.Blinking,
            ToolbarStyle = ToolbarStyle.IconsOnly,
        };

        var theme = ThemeResolver.Resolve(settings, Desktop());

        await Assert.That(theme.Scheme).IsEqualTo(ColorSchemes.Light);
        await Assert.That(theme.PageTone).IsEqualTo(PageTone.None);
        await Assert.That(theme.ReduceMotion).IsTrue();
        await Assert.That(theme.SteadyCaret).IsFalse();
        await Assert.That(theme.ShowLabels).IsFalse();
    }

    /// <summary>Verifies turning page tones off gives plain pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ToneCanBeDisabled() =>
        await Assert.That(ThemeResolver.Resolve(new AppSettings { PageToneEnabled = false }, null).PageTone.IsIdentity).IsTrue();

    /// <summary>Creates default settings.</summary>
    /// <returns>The settings.</returns>
    private static AppSettings Defaults() => new();

    /// <summary>Creates a Breeze Dark like palette.</summary>
    /// <returns>The palette.</returns>
    private static DesktopPalette Desktop() => new()
    {
        WindowBackground = DarkWindow,
        WindowForeground = LightText,
        ViewBackground = DarkView,
        ViewForeground = LightText,
        ButtonBackground = DarkWindow,
        ButtonForeground = LightText,
        HeaderBackground = DarkWindow,
        Accent = 0xFF3DAEE9U,
        AccentForeground = LightText,
        InactiveForeground = 0xFFA1A9B1U,
        IsDark = true,
    };
}
