// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.Core.Printing;

/// <summary>
/// Where a page goes on a printer's printable area: at its true size when it fits, otherwise shrunk to fit, centred.
/// The page is rendered at the device's horizontal resolution and stretched vertically when the vertical one differs.
/// </summary>
/// <param name="Scale">The render scale in pixels per point.</param>
/// <param name="Width">The rendered and printed width in device pixels.</param>
/// <param name="Height">The rendered height in pixels.</param>
/// <param name="DeviceHeight">The printed height in device pixels.</param>
/// <param name="Left">The left edge on the printable area.</param>
/// <param name="Top">The top edge on the printable area.</param>
[DebuggerDisplay("DevicePlacement: {Width} x {DeviceHeight} at ({Left}, {Top})")]
public readonly record struct DevicePlacement(float Scale, int Width, int Height, int DeviceHeight, int Left, int Top)
{
    /// <summary>Divides the spare space either side when centring.</summary>
    private const int Halves = 2;

    /// <summary>Points per inch.</summary>
    private const float PointsPerInch = 72F;

    /// <summary>Fits a page onto a printable area.</summary>
    /// <param name="page">The page size in points.</param>
    /// <param name="dpiX">The horizontal resolution.</param>
    /// <param name="dpiY">The vertical resolution.</param>
    /// <param name="printableWidth">The printable width in device pixels.</param>
    /// <param name="printableHeight">The printable height in device pixels.</param>
    /// <returns>The placement.</returns>
    public static DevicePlacement Fit(PageSize page, int dpiX, int dpiY, int printableWidth, int printableHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dpiX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dpiY);
        var naturalWidth = page.Width / PointsPerInch * dpiX;
        var naturalHeight = page.Height / PointsPerInch * dpiY;
        var shrink = Math.Min(1F, Math.Min(printableWidth / naturalWidth, printableHeight / naturalHeight));
        var scale = dpiX / PointsPerInch * shrink;
        var width = Math.Max(1, (int)MathF.Round(page.Width * scale));
        var height = Math.Max(1, (int)MathF.Round(page.Height * scale));
        var deviceHeight = Math.Max(1, (int)MathF.Round(naturalHeight * shrink));
        return new(scale, width, height, deviceHeight, (printableWidth - width) / Halves, (printableHeight - deviceHeight) / Halves);
    }

    /// <summary>Gets the rows rendered at a time so a band stays within a memory budget.</summary>
    /// <param name="budgetBytes">The most bytes a band may use.</param>
    /// <returns>The rows per band, at least one.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int RowsPerBand(int budgetBytes) => Math.Clamp(budgetBytes / (Width * sizeof(int)), 1, Height);

    /// <summary>Gets the device row a rendered row is printed at, allowing for a different vertical resolution.</summary>
    /// <param name="row">The rendered row.</param>
    /// <returns>The device row, relative to <see cref="Top"/>.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public int DeviceRow(int row) => (int)((long)row * DeviceHeight / Height);
}
