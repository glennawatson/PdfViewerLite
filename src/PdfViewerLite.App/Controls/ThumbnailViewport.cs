// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.App.Controls;

/// <summary>Calculates whole-viewport thumbnail navigation without moving visible items.</summary>
internal static class ThumbnailViewport
{
    /// <summary>Gets the offset that reveals an offscreen thumbnail by whole viewports.</summary>
    /// <param name="offset">The current vertical offset.</param>
    /// <param name="viewport">The viewport height.</param>
    /// <param name="top">The thumbnail's top in content coordinates.</param>
    /// <param name="bottom">The thumbnail's bottom in content coordinates.</param>
    /// <param name="extent">The content height.</param>
    /// <returns>The unchanged offset for visible items, otherwise a clamped page movement.</returns>
    internal static double GetOffset(double offset, double viewport, double top, double bottom, double extent)
    {
        if (viewport <= 0 || (bottom > offset && top < offset + viewport))
        {
            return offset;
        }

        var pages = bottom <= offset ? -Math.Floor((offset - bottom) / viewport) - 1 : Math.Floor((top - offset) / viewport);
        return Math.Clamp(offset + (pages * viewport), 0, Math.Max(0, extent - viewport));
    }
}
