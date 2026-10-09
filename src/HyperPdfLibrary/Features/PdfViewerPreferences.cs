// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>The viewer preferences and the page mode and layout the document asks for. Each property holds the PDF default when the file does not set it.</summary>
[DebuggerDisplay("PdfViewerPreferences: {PageMode} {PageLayout}")]
public sealed record PdfViewerPreferences
{
    /// <summary>The page boundary used when a preference names none.</summary>
    private const string DefaultBoundary = "CropBox";

    /// <summary>Gets the page layout (<c>/PageLayout</c>): SinglePage, OneColumn, TwoColumnLeft, TwoColumnRight, TwoPageLeft or TwoPageRight.</summary>
    public string PageLayout { get; init; } = "SinglePage";

    /// <summary>Gets the page mode (<c>/PageMode</c>): UseNone, UseOutlines, UseThumbs, FullScreen, UseOC or UseAttachments.</summary>
    public string PageMode { get; init; } = "UseNone";

    /// <summary>Gets a value indicating whether to hide the toolbars.</summary>
    public bool HideToolbar { get; init; }

    /// <summary>Gets a value indicating whether to hide the menu bar.</summary>
    public bool HideMenubar { get; init; }

    /// <summary>Gets a value indicating whether to hide window controls.</summary>
    public bool HideWindowUI { get; init; }

    /// <summary>Gets a value indicating whether to fit the window to the first page.</summary>
    public bool FitWindow { get; init; }

    /// <summary>Gets a value indicating whether to centre the window on screen.</summary>
    public bool CenterWindow { get; init; }

    /// <summary>Gets a value indicating whether the title bar shows the document title.</summary>
    public bool DisplayDocTitle { get; init; }

    /// <summary>Gets the page mode to return to when leaving full screen.</summary>
    public string NonFullScreenPageMode { get; init; } = "UseNone";

    /// <summary>Gets the reading direction: L2R or R2L.</summary>
    public string Direction { get; init; } = "L2R";

    /// <summary>Gets the page boundary shown on screen (<c>/ViewArea</c>).</summary>
    public string ViewArea { get; init; } = DefaultBoundary;

    /// <summary>Gets the page boundary that clips on screen (<c>/ViewClip</c>).</summary>
    public string ViewClip { get; init; } = DefaultBoundary;

    /// <summary>Gets the page boundary printed (<c>/PrintArea</c>).</summary>
    public string PrintArea { get; init; } = DefaultBoundary;

    /// <summary>Gets the page boundary that clips on paper (<c>/PrintClip</c>).</summary>
    public string PrintClip { get; init; } = DefaultBoundary;

    /// <summary>Gets the print scaling: AppDefault or None.</summary>
    public string PrintScaling { get; init; } = "AppDefault";

    /// <summary>Gets the duplex mode (Simplex, DuplexFlipShortEdge or DuplexFlipLongEdge), or null.</summary>
    public string? Duplex { get; init; }

    /// <summary>Gets a value indicating whether to pick the paper tray by the page size.</summary>
    public bool PickTrayByPdfSize { get; init; }

    /// <summary>Gets the page ranges to print, as first and last page pairs (one based), or an empty array.</summary>
    public int[] PrintPageRange { get; init; } = [];

    /// <summary>Gets the number of copies to print; 1 when not set.</summary>
    public int NumCopies { get; init; } = 1;

    /// <summary>Gets the names of the preferences the viewer must honour (<c>/Enforce</c>).</summary>
    public string[] Enforce { get; init; } = [];
}
