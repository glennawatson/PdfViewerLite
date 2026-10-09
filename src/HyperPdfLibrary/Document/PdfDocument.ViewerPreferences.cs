// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>Viewer preferences, page mode and layout, and page transitions.</content>
public sealed partial class PdfDocument
{
    /// <summary>The page mode when the catalog names none.</summary>
    private const string DefaultPageMode = "UseNone";

    /// <summary>The page boundary used when a preference names none.</summary>
    private const string DefaultBoundary = "CropBox";

    /// <summary>The transition style when a transition dictionary names none.</summary>
    private const string DefaultTransitionStyle = "R";

    /// <summary>The default direction of a transition's name <c>None</c>.</summary>
    private const int NoDirection = -1;

    /// <summary>Gets the viewer preferences with the page mode and layout. Missing entries keep the PDF defaults.</summary>
    /// <returns>The preferences.</returns>
    public PdfViewerPreferences GetViewerPreferences()
    {
        var prefs = Catalog.GetDictionary(KnownName.ViewerPreferences);
        return new()
        {
            PageLayout = Catalog.NameText("PageLayout") ?? "SinglePage",
            PageMode = Catalog.NameText("PageMode") ?? DefaultPageMode,
            HideToolbar = PreferenceFlag(prefs, "HideToolbar"),
            HideMenubar = PreferenceFlag(prefs, "HideMenubar"),
            HideWindowUI = PreferenceFlag(prefs, "HideWindowUI"),
            FitWindow = PreferenceFlag(prefs, "FitWindow"),
            CenterWindow = PreferenceFlag(prefs, "CenterWindow"),
            DisplayDocTitle = PreferenceFlag(prefs, "DisplayDocTitle"),
            NonFullScreenPageMode = PreferenceName(prefs, "NonFullScreenPageMode", DefaultPageMode),
            Direction = PreferenceName(prefs, "Direction", "L2R"),
            ViewArea = PreferenceName(prefs, "ViewArea", DefaultBoundary),
            ViewClip = PreferenceName(prefs, "ViewClip", DefaultBoundary),
            PrintArea = PreferenceName(prefs, "PrintArea", DefaultBoundary),
            PrintClip = PreferenceName(prefs, "PrintClip", DefaultBoundary),
            PrintScaling = PreferenceName(prefs, "PrintScaling", "AppDefault"),
            Duplex = prefs?.NameText("Duplex"),
            PickTrayByPdfSize = PreferenceFlag(prefs, "PickTrayByPDFSize"),
            PrintPageRange = ToInt32Array(prefs?.Array("PrintPageRange")),
            NumCopies = prefs?.Int("NumCopies", 1) ?? 1,
            Enforce = prefs?.Array("Enforce").NameTexts() ?? [],
        };
    }

    /// <summary>Gets a page's transition (<c>/Trans</c>) with its automatic advance time (<c>/Dur</c>).</summary>
    /// <param name="page">The page.</param>
    /// <returns>The transition, or <see langword="null"/> when the page has none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public PdfPageTransition? GetTransition(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var advance = page.Dictionary.Value("Dur");
        return page.Dictionary.Dict("Trans") is { } trans ? ReadTransition(trans, advance.IsNumber ? advance.AsNumber() : null) : null;
    }

    /// <summary>Reads a transition dictionary.</summary>
    /// <param name="trans">The transition dictionary.</param>
    /// <param name="advanceAfter">The page's automatic advance time in seconds, or null.</param>
    /// <returns>The transition.</returns>
    internal static PdfPageTransition ReadTransition(PdfDictionary trans, double? advanceAfter)
    {
        var direction = trans.Value("Di");
        var degrees = direction.Kind == PdfKind.Name ? NoDirection : direction.AsInt32();
        return new(
            trans.NameText("S") ?? DefaultTransitionStyle,
            trans.Num("D", 1),
            trans.NameText("Dm") ?? "H",
            trans.NameText("M") ?? "I",
            degrees,
            trans.Num("SS", 1),
            trans.Flag("B", false),
            advanceAfter);
    }

    /// <summary>Reads a boolean viewer preference.</summary>
    /// <param name="prefs">The preferences dictionary, or null.</param>
    /// <param name="key">The key.</param>
    /// <returns>The value; false when missing.</returns>
    private static bool PreferenceFlag(PdfDictionary? prefs, string key) => prefs?.Flag(key, false) ?? false;

    /// <summary>Reads a name viewer preference.</summary>
    /// <param name="prefs">The preferences dictionary, or null.</param>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The value when missing.</param>
    /// <returns>The name text.</returns>
    private static string PreferenceName(PdfDictionary? prefs, string key, string fallback) => prefs?.NameText(key) ?? fallback;
}
