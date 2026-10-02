// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Core.Platform;

/// <summary>Supplies the desktop colour scheme and reports changes.</summary>
public interface IDesktopThemeSource
{
    /// <summary>Raised when the palette changes.</summary>
    event EventHandler? PaletteChanged;

    /// <summary>Gets the current palette, or <see langword="null"/> when the desktop offers none.</summary>
    DesktopPalette? Palette { get; }
}
