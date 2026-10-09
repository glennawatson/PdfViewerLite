// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Graphics;

/// <summary>
/// A fill or stroke colour, resolved to RGB when it is set so painting never converts colour. A pattern colour keeps the
/// pattern too, and its RGB is the underlying colour of an uncoloured pattern.
/// </summary>
/// <param name="Space">The colour space.</param>
/// <param name="Rgb">The colour as 0xRRGGBB.</param>
/// <param name="Pattern">The pattern, when the space is a pattern space and a pattern is selected.</param>
/// <param name="PaintsNothing">Whether the colour marks nothing, as the Separation colourant /None does.</param>
[DebuggerDisplay("ColorState: {Rgb:X6}")]
internal readonly record struct ColorState(PdfColorSpace Space, uint Rgb, PdfPatternPaint? Pattern, bool PaintsNothing)
{
    /// <summary>The bits per RGB channel.</summary>
    private const int ChannelBits = 8;

    /// <summary>The largest channel value.</summary>
    private const float ChannelMax = 255;

    /// <summary>The shift of the red channel.</summary>
    private const int RedShift = 16;

    /// <summary>The position of blue in an RGB triple.</summary>
    private const int BlueIndex = 2;

    /// <summary>Gets black in DeviceGray, the initial colour.</summary>
    internal static ColorState Black => new(PdfColorSpace.DeviceGray, 0, null, false);

    /// <summary>Gets the red channel, 0 to 255.</summary>
    internal byte Red => (byte)(Rgb >>> RedShift);

    /// <summary>Gets the green channel, 0 to 255.</summary>
    internal byte Green => (byte)(Rgb >>> ChannelBits);

    /// <summary>Gets the blue channel, 0 to 255.</summary>
    internal byte Blue => (byte)Rgb;

    /// <summary>Resolves components in a colour space.</summary>
    /// <param name="space">The colour space.</param>
    /// <param name="components">The components.</param>
    /// <returns>The colour.</returns>
    internal static ColorState Resolve(PdfColorSpace space, ReadOnlySpan<float> components)
    {
        Span<float> rgb = stackalloc float[PdfColorSpace.RgbComponents];
        space.ToRgb(components, rgb);
        return new(space, Pack(rgb), null, space.PaintsNothing);
    }

    /// <summary>Packs RGB components into 0xRRGGBB.</summary>
    /// <param name="rgb">Red, green and blue from 0 to 1.</param>
    /// <returns>The packed colour.</returns>
    internal static uint Pack(ReadOnlySpan<float> rgb) =>
        (Channel(rgb[0]) << RedShift) | (Channel(rgb[1]) << ChannelBits) | Channel(rgb[BlueIndex]);

    /// <summary>Converts one channel to a byte.</summary>
    /// <param name="value">The channel from 0 to 1.</param>
    /// <returns>The byte value.</returns>
    private static uint Channel(float value) => (uint)MathF.Round(Math.Clamp(value, 0, 1) * ChannelMax);
}
