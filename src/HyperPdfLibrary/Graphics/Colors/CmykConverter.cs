// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// Converts CMYK to sRGB with the same table interpolation as PDFium's AdobeCmykToStandardRgb
/// (core/fxge/dib/cfx_cmyk_to_srgb.cpp, BSD-3-Clause; original code copyright 2014 Foxit Software Inc.).
/// The result starts at the nearest grid point of <see cref="CmykTable"/> and adds a linear step toward the neighbouring
/// grid point on each of the four axes.
/// </summary>
internal static class CmykConverter
{
    /// <summary>The bits that scale a byte to the fixed-point grid position.</summary>
    private const int SampleShift = 8;

    /// <summary>The bits between grid points: 2^13 is 32 bytes of the 0..255 range, in fixed point.</summary>
    private const int CellShift = 13;

    /// <summary>Half a grid cell, which rounds to the nearest grid point.</summary>
    private const int HalfCell = 4096;

    /// <summary>The index of the last grid point on an axis.</summary>
    private const int LastGridIndex = 8;

    /// <summary>The divisor of each interpolation step.</summary>
    private const int RateDivisor = 32;

    /// <summary>The grid points per axis.</summary>
    private const int GridSize = 9;

    /// <summary>The grid stride of the cyan axis.</summary>
    private const int CyanStride = GridSize * GridSize * GridSize;

    /// <summary>The grid stride of the magenta axis.</summary>
    private const int MagentaStride = GridSize * GridSize;

    /// <summary>The grid stride of the yellow axis.</summary>
    private const int YellowStride = GridSize;

    /// <summary>The bytes per grid point.</summary>
    private const int BytesPerPoint = 3;

    /// <summary>The green byte offset.</summary>
    private const int GreenOffset = 1;

    /// <summary>The blue byte offset.</summary>
    private const int BlueOffset = 2;

    /// <summary>The float that precedes 0.5, which rounds a 0..255 float like PDFium.</summary>
    private const float RoundingOffset = 0.49999997F;

    /// <summary>The shift of the red byte in a BGRA word.</summary>
    private const int RedShift = 16;

    /// <summary>The shift of the green byte in a BGRA word.</summary>
    private const int GreenShift = 8;

    /// <summary>The byte scale.</summary>
    private const float ByteScale = 255F;

    /// <summary>Converts a CMYK colour to a little-endian BGRA word.</summary>
    /// <param name="cyan">The cyan byte.</param>
    /// <param name="magenta">The magenta byte.</param>
    /// <param name="yellow">The yellow byte.</param>
    /// <param name="black">The black byte.</param>
    /// <returns>The opaque BGRA word, to be written little-endian.</returns>
    internal static uint ToBgra(byte cyan, byte magenta, byte yellow, byte black)
    {
        var fixC = cyan << SampleShift;
        var fixM = magenta << SampleShift;
        var fixY = yellow << SampleShift;
        var fixK = black << SampleShift;
        var ci = NearestIndex(fixC);
        var mi = NearestIndex(fixM);
        var yi = NearestIndex(fixY);
        var ki = NearestIndex(fixK);
        var start = ((CyanStride * ci) + (MagentaStride * mi) + (YellowStride * yi) + ki) * BytesPerPoint;
        var data = CmykTable.Data;
        Span<int> sum = stackalloc int[BytesPerPoint];
        sum[0] = data[start] << SampleShift;
        sum[GreenOffset] = data[start + GreenOffset] << SampleShift;
        sum[BlueOffset] = data[start + BlueOffset] << SampleShift;
        Step(data, start, CyanStride, fixC, ci, sum);
        Step(data, start, MagentaStride, fixM, mi, sum);
        Step(data, start, YellowStride, fixY, yi, sum);
        Step(data, start, 1, fixK, ki, sum);
        return PixelConverter.Pack(ToChannel(sum[0]), ToChannel(sum[GreenOffset]), ToChannel(sum[BlueOffset]));
    }

    /// <summary>Converts a CMYK colour given as floats from 0 to 1 to RGB floats from 0 to 1.</summary>
    /// <param name="cyan">The cyan value.</param>
    /// <param name="magenta">The magenta value.</param>
    /// <param name="yellow">The yellow value.</param>
    /// <param name="black">The black value.</param>
    /// <param name="rgb">Receives red, green and blue.</param>
    internal static void ToRgb(float cyan, float magenta, float yellow, float black, Span<float> rgb)
    {
        var word = ToBgra(ToSample(cyan), ToSample(magenta), ToSample(yellow), ToSample(black));
        rgb[0] = ((word >> RedShift) & byte.MaxValue) / ByteScale;
        rgb[1] = ((word >> GreenShift) & byte.MaxValue) / ByteScale;
        rgb[2] = (word & byte.MaxValue) / ByteScale;
    }

    /// <summary>Converts a float from 0 to 1 to a byte; out-of-range and NaN values clamp.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ToSample(float value)
    {
        if (!(value > 0))
        {
            return 0;
        }

        return value >= 1 ? byte.MaxValue : (byte)((value * ByteScale) + RoundingOffset);
    }

    /// <summary>Gets the nearest grid index of a fixed-point value.</summary>
    /// <param name="fix">The value shifted left by 8.</param>
    /// <returns>The grid index from 0 to 8.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NearestIndex(int fix) => (fix + HalfCell) >> CellShift;

    /// <summary>Adds the interpolation step toward the neighbouring grid point on one axis.</summary>
    /// <param name="data">The table.</param>
    /// <param name="start">The byte offset of the nearest grid point.</param>
    /// <param name="stride">The grid stride of the axis.</param>
    /// <param name="fix">The fixed-point value on the axis.</param>
    /// <param name="index">The nearest grid index on the axis.</param>
    /// <param name="sum">The red, green and blue accumulators.</param>
    private static void Step(ReadOnlySpan<byte> data, int start, int stride, int fix, int index, Span<int> sum)
    {
        var neighbour = fix >> CellShift;
        if (neighbour == index)
        {
            neighbour = neighbour == LastGridIndex ? neighbour - 1 : neighbour + 1;
        }

        var other = start + ((neighbour - index) * stride * BytesPerPoint);
        var rate = (fix - (index << CellShift)) * (index - neighbour);
        sum[0] += (data[start] - data[other]) * rate / RateDivisor;
        sum[GreenOffset] += (data[start + GreenOffset] - data[other + GreenOffset]) * rate / RateDivisor;
        sum[BlueOffset] += (data[start + BlueOffset] - data[other + BlueOffset]) * rate / RateDivisor;
    }

    /// <summary>Converts an accumulator to a byte.</summary>
    /// <param name="value">The fixed-point accumulator.</param>
    /// <returns>The byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ToChannel(int value) => (byte)Math.Min(Math.Max(value, 0) >> SampleShift, byte.MaxValue);
}
