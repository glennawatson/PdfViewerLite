// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Writes decoded channels as interleaved 8-bit samples, the way PDFium's <c>CJPX_Decoder::Decode</c> scales them:
/// narrower samples shift up, wider ones round down to eight bits. Palettes apply on the way, sYCC converts to RGB as
/// PDFium does, and channels smaller than the first are stretched to its size.
/// </summary>
internal static class JpxSampleWriter
{
    /// <summary>The bits of an output sample.</summary>
    private const int SampleBits = 8;

    /// <summary>The modulus that picks the bit below the kept ones, PDFium's rounding term.</summary>
    private const int RoundingModulus = 2;

    /// <summary>The largest output sample.</summary>
    private const int MaxSample = 255;

    /// <summary>The second chroma channel.</summary>
    private const int SecondChroma = 2;

    /// <summary>The sYCC red coefficient of Cr.</summary>
    private const double RedFromCr = 1.402;

    /// <summary>The sYCC green coefficient of Cb.</summary>
    private const double GreenFromCb = 0.344;

    /// <summary>The sYCC green coefficient of Cr.</summary>
    private const double GreenFromCr = 0.714;

    /// <summary>The sYCC blue coefficient of Cb.</summary>
    private const double BlueFromCb = 1.772;

    /// <summary>The samples per pixel of the RGB part.</summary>
    private const int RgbComponents = 3;

    /// <summary>Writes the planned components.</summary>
    /// <param name="source">The decoded planes, channels and palette.</param>
    /// <param name="plan">The output plan.</param>
    /// <param name="samples">Receives the interleaved samples.</param>
    internal static void Write(in JpxSampleSource source, JpxOutputPlan plan, Span<byte> samples)
    {
        var first = 0;
        if (plan.Sycc != JpxSyccLayout.None)
        {
            WriteSycc(source, plan.Sycc, plan.Components, samples);
            first = RgbComponents;
        }

        for (var o = first; o < plan.Components; o++)
        {
            WriteChannel(source, source.Channels[o], o, plan.Components, samples);
        }
    }

    /// <summary>Writes one channel as single 8-bit samples, for an alpha plane.</summary>
    /// <param name="source">The decoded planes, channels and palette.</param>
    /// <param name="channel">The channel index.</param>
    /// <param name="alpha">Receives one sample per pixel.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteAlpha(in JpxSampleSource source, int channel, Span<byte> alpha) =>
        WriteChannel(source, source.Channels[channel], 0, 1, alpha);

    /// <summary>Mixes colour samples toward white by their alpha, as PDFium does for <c>/SMaskInData 1</c>.</summary>
    /// <param name="samples">The RGB samples, updated in place.</param>
    /// <param name="alpha">The alpha of each pixel.</param>
    internal static void BlendToWhite(Span<byte> samples, ReadOnlySpan<byte> alpha)
    {
        for (var i = 0; i < alpha.Length; i++)
        {
            var a = alpha[i];
            var white = MaxSample * (MaxSample - a);
            var pixel = samples.Slice(i * RgbComponents, RgbComponents);
            pixel[0] = (byte)(((pixel[0] * a) + white) / MaxSample);
            pixel[1] = (byte)(((pixel[1] * a) + white) / MaxSample);
            pixel[SecondChroma] = (byte)(((pixel[SecondChroma] * a) + white) / MaxSample);
        }
    }

    /// <summary>Scales one value to eight bits as PDFium does.</summary>
    /// <param name="value">The value.</param>
    /// <param name="precision">The value's bits.</param>
    /// <param name="signed">Whether the value is signed.</param>
    /// <returns>The 8-bit sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte ToByte(int value, int precision, bool signed)
    {
        var source = value + (signed ? 1 << (precision - 1) : 0);
        var adjust = precision - SampleBits;
        if (adjust <= 0)
        {
            return (byte)(source << -adjust);
        }

        var rounded = (source >> adjust) + ((source >> (adjust - 1)) % RoundingModulus);
        return (byte)Math.Clamp(rounded, 0, MaxSample);
    }

    /// <summary>Writes one channel into every <paramref name="stride"/>-th sample from <paramref name="offset"/>.</summary>
    /// <param name="source">The decoded planes, channels and palette.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="offset">The sample within a pixel.</param>
    /// <param name="stride">The samples per pixel.</param>
    /// <param name="samples">The output.</param>
    private static void WriteChannel(in JpxSampleSource source, in JpxChannel channel, int offset, int stride, Span<byte> samples)
    {
        var plane = source.Image.Planes[channel.Component];
        var area = source.Image.Areas[channel.Component];
        var direct = channel is { Column: < 0, Precision: SampleBits, Signed: false } && area.Width == source.Width && area.Height == source.Height;
        for (var y = 0; y < source.Height; y++)
        {
            var output = samples.Slice(y * source.Width * stride);
            var row = (int)((long)y * area.Height / source.Height);
            var line = plane.AsSpan(row * area.Width, area.Width);
            if (direct)
            {
                WriteDirectRow(line, output, offset, stride);
            }
            else
            {
                WriteRow(source, channel, line, output, offset, stride);
            }
        }
    }

    /// <summary>Writes a row of 8-bit unsigned values that need no scaling.</summary>
    /// <param name="line">The plane row.</param>
    /// <param name="output">The output from the row's first pixel.</param>
    /// <param name="offset">The sample within a pixel.</param>
    /// <param name="stride">The samples per pixel.</param>
    private static void WriteDirectRow(ReadOnlySpan<int> line, Span<byte> output, int offset, int stride)
    {
        var position = offset;
        foreach (var value in line)
        {
            output[position] = (byte)value;
            position += stride;
        }
    }

    /// <summary>Writes a row that needs a palette, scaling or stretching.</summary>
    /// <param name="source">The decoded planes, channels and palette.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="line">The plane row.</param>
    /// <param name="output">The output from the row's first pixel.</param>
    /// <param name="offset">The sample within a pixel.</param>
    /// <param name="stride">The samples per pixel.</param>
    private static void WriteRow(in JpxSampleSource source, in JpxChannel channel, ReadOnlySpan<int> line, Span<byte> output, int offset, int stride)
    {
        var palette = channel.Column >= 0 ? source.Palette : null;
        for (var x = 0; x < source.Width; x++)
        {
            var value = line[(int)((long)x * line.Length / source.Width)];
            if (palette is not null)
            {
                value = palette.Get(value, channel.Column);
            }

            output[(x * stride) + offset] = ToByte(value, channel.Precision, channel.Signed);
        }
    }

    /// <summary>Converts sYCC to RGB as PDFium's <c>sycc_to_rgb</c> does and writes the first three samples of each pixel.</summary>
    /// <param name="source">The decoded planes, channels and palette.</param>
    /// <param name="layout">The chroma sampling.</param>
    /// <param name="stride">The samples per pixel.</param>
    /// <param name="samples">The output.</param>
    private static void WriteSycc(in JpxSampleSource source, JpxSyccLayout layout, int stride, Span<byte> samples)
    {
        var channels = source.Channels;
        var luma = source.Image.Planes[channels[0].Component];
        var blue = source.Image.Planes[channels[1].Component];
        var red = source.Image.Planes[channels[SecondChroma].Component];
        var chroma = source.Image.Areas[channels[1].Component];
        var precision = channels[0].Precision;
        var offset = 1 << (precision - 1);
        var upper = (1 << precision) - 1;
        var shiftX = layout == JpxSyccLayout.Full ? 0 : 1;
        var shiftY = layout == JpxSyccLayout.Quarter ? 1 : 0;
        for (var y = 0; y < source.Height; y++)
        {
            var chromaRow = Math.Min(y >> shiftY, chroma.Height - 1) * chroma.Width;
            for (var x = 0; x < source.Width; x++)
            {
                var c = chromaRow + Math.Min(x >> shiftX, chroma.Width - 1);
                var l = luma[(y * source.Width) + x];
                var cb = blue[c] - offset;
                var cr = red[c] - offset;
                var pixel = samples.Slice(((y * source.Width) + x) * stride, RgbComponents);
                pixel[0] = ToByte(Math.Clamp(l + (int)(RedFromCr * cr), 0, upper), channels[0].Precision, channels[0].Signed);
                pixel[1] = ToByte(Math.Clamp(l - (int)((GreenFromCb * cb) + (GreenFromCr * cr)), 0, upper), channels[1].Precision, channels[1].Signed);
                pixel[SecondChroma] = ToByte(Math.Clamp(l + (int)(BlueFromCb * cb), 0, upper), channels[SecondChroma].Precision, channels[SecondChroma].Signed);
            }
        }
    }
}
