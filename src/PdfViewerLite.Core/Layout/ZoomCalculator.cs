// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.Core.Layout;

/// <summary>Computes zoom scales.</summary>
public static class ZoomCalculator
{
    /// <summary>Device independent pixels per point at 100% zoom.</summary>
    public static readonly double PixelsPerPoint = 96.0 / 72.0;

    /// <summary>The smallest zoom factor.</summary>
    public static readonly double MinZoom = 0.05;

    /// <summary>The largest zoom factor.</summary>
    public static readonly double MaxZoom = 64.0;

    /// <summary>The number of pages in a two page spread.</summary>
    private const int SpreadPages = 2;

    /// <summary>The number of sides a margin applies to along one axis.</summary>
    private const double Sides = 2.0;

    /// <summary>Tolerance when comparing zoom factors.</summary>
    private const double Epsilon = 0.001;

    /// <summary>The preset zoom factors the zoom in and out commands step through.</summary>
    private static readonly double[] Steps = [0.1, 0.25, 0.333, 0.5, 0.667, 0.75, 0.9, 1.0, 1.1, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0, 4.0, 6.0, 8.0, 12.0, 16.0, 24.0, 32.0, 48.0, 64.0];

    /// <summary>Gets the zoom factor (1 = 100%) that fits pages to the viewport.</summary>
    /// <param name="sizes">The page sizes.</param>
    /// <param name="parameters">The viewport and arrangement.</param>
    /// <returns>The zoom factor; 1 when the zoom mode is <see cref="ZoomMode.Free"/>.</returns>
    public static double GetFitZoom(ReadOnlySpan<PageSize> sizes, in FitParameters parameters)
    {
        if (parameters.ZoomMode == ZoomMode.Free || sizes.IsEmpty || parameters.ViewportWidth <= 0 || parameters.ViewportHeight <= 0)
        {
            return 1.0;
        }

        var columns = parameters.Mode == PageLayoutMode.Single ? 1 : SpreadPages;
        var maxWidth = 0.0;
        var maxHeight = 0.0;
        foreach (var size in sizes)
        {
            var rotated = size.Rotate(parameters.Rotation);
            maxWidth = Math.Max(maxWidth, rotated.Width);
            maxHeight = Math.Max(maxHeight, rotated.Height);
        }

        var availableWidth = Math.Max(1, parameters.ViewportWidth - (Sides * parameters.Margin) - (parameters.Spacing * (columns - 1)));
        var scale = availableWidth / (maxWidth * columns);
        if (parameters.ZoomMode == ZoomMode.FitPage)
        {
            var availableHeight = Math.Max(1, parameters.ViewportHeight - (Sides * parameters.Margin));
            scale = Math.Min(scale, availableHeight / maxHeight);
        }

        return Clamp(scale / PixelsPerPoint);
    }

    /// <summary>Gets the next larger preset zoom.</summary>
    /// <param name="zoom">The current zoom.</param>
    /// <returns>The larger zoom.</returns>
    public static double ZoomIn(double zoom)
    {
        foreach (var step in Steps)
        {
            if (step > zoom + Epsilon)
            {
                return step;
            }
        }

        return MaxZoom;
    }

    /// <summary>Gets the next smaller preset zoom.</summary>
    /// <param name="zoom">The current zoom.</param>
    /// <returns>The smaller zoom.</returns>
    public static double ZoomOut(double zoom)
    {
        for (var i = Steps.Length - 1; i >= 0; i--)
        {
            if (Steps[i] < zoom - Epsilon)
            {
                return Steps[i];
            }
        }

        return MinZoom;
    }

    /// <summary>Clamps a zoom factor to the supported range.</summary>
    /// <param name="zoom">The zoom.</param>
    /// <returns>The clamped zoom.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Clamp(double zoom) => Math.Clamp(zoom, MinZoom, MaxZoom);
}
