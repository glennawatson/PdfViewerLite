// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The embedded block decoder, tier 1 (ISO 15444-1 annex D): significance propagation, magnitude refinement and cleanup
/// passes over a code-block's bit-planes, with the bypass, reset, termination, vertically causal and segmentation symbol
/// mode switches. Coefficients are rebuilt at twice their scale with the mid-point of the remaining interval, as
/// PDFium does, then halved (5/3) or multiplied by half the step (9/7) into the tile-component.
/// </summary>
/// <remarks>
/// The state is stored stripe by stripe: the four rows of a stripe column are adjacent bytes, so one 32-bit read tests a
/// whole column. One instance serves one thread and reuses its buffers for every code-block.
/// </remarks>
[DebuggerDisplay("JpxBlockDecoder: {_width}x{_height}")]
internal sealed partial class JpxBlockDecoder : IDisposable
{
    /// <summary>The rows of a stripe.</summary>
    private const int StripeRows = 4;

    /// <summary>The last row of a stripe.</summary>
    private const int LastRow = StripeRows - 1;

    /// <summary>The highest bit-plane the 32-bit coefficients can hold at twice scale.</summary>
    private const int MaxPlanes = 31;

    /// <summary>The bit-planes coded with the arithmetic coder before the bypass mode switches to raw passes.</summary>
    private const int BypassStartPlanes = 4;

    /// <summary>The cleanup pass number within a bit-plane.</summary>
    private const int CleanupPass = 2;

    /// <summary>The passes of a bit-plane.</summary>
    private const int PassesPerPlane = 3;

    /// <summary>The smallest state buffer rented.</summary>
    private const int MinimumState = 4096;

    /// <summary>The padding columns, and padding stripes, around a code-block's state: one on each side.</summary>
    private const int Border = 2;

    /// <summary>The scale the coefficients are rebuilt at.</summary>
    private const int Scale = 2;

    /// <summary>The context states.</summary>
    private readonly byte[] _contexts = new byte[JpxContexts.Count];

    /// <summary>The coefficients, twice scale, in stripe order.</summary>
    private int[] _values = [];

    /// <summary>The coefficient flags, in stripe order.</summary>
    private byte[] _flags = [];

    /// <summary>The neighbourhood codes, in stripe order.</summary>
    private byte[] _neighbours = [];

    /// <summary>The joined code-block data when it arrived in pieces.</summary>
    private byte[] _joined = [];

    /// <summary>The code-block width.</summary>
    private int _width;

    /// <summary>The code-block height.</summary>
    private int _height;

    /// <summary>The number of stripes.</summary>
    private int _stripes;

    /// <summary>The distance between stripes in the state buffers.</summary>
    private int _stripeStride;

    /// <summary>The used length of the state buffers.</summary>
    private int _length;

    /// <summary>The offset of the current orientation's zero-coding table.</summary>
    private int _zeroTable;

    /// <summary>Whether the vertically causal mode is on.</summary>
    private bool _causal;

    /// <inheritdoc/>
    public void Dispose()
    {
        Return(ref _values);
        Return(ref _flags);
        Return(ref _neighbours);
        Return(ref _joined);
    }

    /// <summary>Decodes one code-block into its tile-component buffer.</summary>
    /// <param name="tile">The tile, which holds the code-block's segments and chunks.</param>
    /// <param name="blockIndex">The code-block index.</param>
    /// <param name="data">The tile's packet data.</param>
    /// <param name="target">Where the coefficients go.</param>
    internal void Decode(JpxTile tile, int blockIndex, byte[] data, in JpxBlockTarget target)
    {
        PdfCancellation.ThrowIfCancelled();
        var block = tile.Blocks[blockIndex];
        var planes = target.RoiShift + block.BitPlanes;
        if (block.DataLength == 0 || block.FirstSegment < 0 || planes >= MaxPlanes || planes < 1 || block.Area.IsEmpty)
        {
            return;
        }

        var band = tile.Bands[block.Band];
        if ((target.Style & JpxBlockStyle.HighThroughput) != 0)
        {
            DecodeHighThroughput(tile, block, band, data, target);
            return;
        }

        Prepare(block.Area.Width, block.Area.Height, band.Orientation, target.Style);
        var source = Gather(tile, block, data, out var offset);
        RunSegments(tile, block, source, offset, target);
        if (target.RoiShift > 0)
        {
            ApplyRoi(target.RoiShift);
        }

        Store(block, band, target);
    }

    /// <summary>Returns a rented array.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array, emptied.</param>
    private static void Return<T>(ref T[] array)
    {
        if (array.Length > 0)
        {
            ScratchPool<T>.Shared.Return(array);
        }

        array = [];
    }

    /// <summary>Makes sure a rented array holds enough elements.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="array">The array, replaced when too small.</param>
    /// <param name="length">The elements needed.</param>
    private static void Ensure<T>(ref T[] array, int length)
    {
        if (array.Length >= length)
        {
            return;
        }

        Return(ref array);
        array = ScratchPool<T>.Shared.Rent(Math.Max(length, MinimumState));
    }

    /// <summary>Reads the four bytes of a stripe column.</summary>
    /// <param name="buffer">The state buffer.</param>
    /// <param name="index">The column's first byte.</param>
    /// <returns>The four bytes as one value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadColumn(byte[] buffer, int index) =>
        Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), index));

    /// <summary>Writes the four bytes of a stripe column.</summary>
    /// <param name="buffer">The state buffer.</param>
    /// <param name="index">The column's first byte.</param>
    /// <param name="value">The four bytes as one value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteColumn(byte[] buffer, int index, uint value) =>
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), index), value);

    /// <summary>Sizes and clears the state for a code-block.</summary>
    /// <param name="width">The code-block width.</param>
    /// <param name="height">The code-block height.</param>
    /// <param name="orientation">The sub-band orientation.</param>
    /// <param name="style">The mode switches.</param>
    private void Prepare(int width, int height, int orientation, JpxBlockStyle style)
    {
        _width = width;
        _height = height;
        _stripes = (height + LastRow) / StripeRows;
        _stripeStride = (width + Border) * StripeRows;
        _length = (_stripes + Border) * _stripeStride;
        Ensure(ref _values, _length);
        Ensure(ref _flags, _length);
        Ensure(ref _neighbours, _length);
        _values.AsSpan(0, _length).Clear();
        _flags.AsSpan(0, _length).Clear();
        _neighbours.AsSpan(0, _length).Clear();
        _zeroTable = orientation * JpxContexts.Neighbourhoods;
        _causal = (style & JpxBlockStyle.VerticallyCausal) != 0;
        ResetContexts();
    }

    /// <summary>Sets every context to its initial state (table D.7).</summary>
    private void ResetContexts()
    {
        _contexts.AsSpan().Clear();
        _contexts[0] = JpxContexts.ZeroState;
        _contexts[JpxContexts.RunLength] = JpxContexts.RunLengthState;
        _contexts[JpxContexts.Uniform] = JpxContexts.UniformState;
    }

    /// <summary>Gets the code-block's data as one run, joining its chunks when there is more than one.</summary>
    /// <param name="tile">The tile.</param>
    /// <param name="block">The code-block.</param>
    /// <param name="data">The tile's packet data.</param>
    /// <param name="offset">Receives the offset of the run.</param>
    /// <returns>The array holding the run.</returns>
    private byte[] Gather(JpxTile tile, JpxCodeBlock block, byte[] data, out int offset)
    {
        var first = tile.Chunks[block.FirstChunk];
        if (first.Next < 0)
        {
            offset = first.Offset;
            return data;
        }

        Ensure(ref _joined, block.DataLength);
        var written = 0;
        for (var chunk = block.FirstChunk; chunk >= 0; chunk = tile.Chunks[chunk].Next)
        {
            var piece = tile.Chunks[chunk];
            data.AsSpan(piece.Offset, piece.Length).CopyTo(_joined.AsSpan(written));
            written += piece.Length;
        }

        offset = 0;
        return _joined;
    }

    /// <summary>Runs the coding passes of every segment, each segment with a fresh arithmetic or raw decoder.</summary>
    /// <param name="tile">The tile.</param>
    /// <param name="block">The code-block.</param>
    /// <param name="source">The coded data.</param>
    /// <param name="offset">The offset of the first segment.</param>
    /// <param name="target">The decoding target, for the mode switches and region-of-interest shift.</param>
    private void RunSegments(JpxTile tile, JpxCodeBlock block, byte[] source, int offset, in JpxBlockTarget target)
    {
        var coder = default(JpxMqDecoder);
        var cursor = new PassCursor(target.RoiShift + block.BitPlanes, CleanupPass);
        var end = offset + block.DataLength;
        var bypass = (target.Style & JpxBlockStyle.Bypass) != 0;
        for (var segment = block.FirstSegment; segment >= 0 && cursor.Plane >= 1; segment = tile.Segments[segment].Next)
        {
            var current = tile.Segments[segment];
            var length = Math.Min(current.Length, end - offset);
            var raw = bypass && cursor.Plane <= block.BitPlanes - BypassStartPlanes && cursor.Pass < CleanupPass;
            if (raw)
            {
                coder.StartRaw(source, offset, length);
            }
            else
            {
                coder.Start(source, offset, length);
            }

            offset += length;
            cursor = RunPasses(ref coder, current.Passes, raw, target.Style, cursor);
        }
    }

    /// <summary>Runs a segment's coding passes.</summary>
    /// <param name="coder">The segment's decoder.</param>
    /// <param name="passes">The passes in the segment.</param>
    /// <param name="raw">Whether the segment is raw bypass data.</param>
    /// <param name="style">The mode switches.</param>
    /// <param name="cursor">The bit-plane and pass to start at.</param>
    /// <returns>The bit-plane and pass after the segment.</returns>
    private PassCursor RunPasses(ref JpxMqDecoder coder, int passes, bool raw, JpxBlockStyle style, PassCursor cursor)
    {
        var (plane, pass) = cursor;
        for (var i = 0; i < passes && plane >= 1; i++)
        {
            RunPass(ref coder, pass, plane, raw, style);
            if (!raw && (style & JpxBlockStyle.Reset) != 0)
            {
                ResetContexts();
            }

            pass++;
            if (pass != PassesPerPlane)
            {
                continue;
            }

            pass = 0;
            plane--;
        }

        return new(plane, pass);
    }

    /// <summary>Runs one coding pass.</summary>
    /// <param name="coder">The decoder.</param>
    /// <param name="pass">The pass type: 0 significance, 1 refinement, 2 cleanup.</param>
    /// <param name="plane">The bit-plane plus one.</param>
    /// <param name="raw">Whether the pass is raw bypass data.</param>
    /// <param name="style">The mode switches.</param>
    private void RunPass(ref JpxMqDecoder coder, int pass, int plane, bool raw, JpxBlockStyle style)
    {
        switch (pass)
        {
            case 0:
            {
                Significance(ref coder, plane, raw);
                break;
            }

            case 1:
            {
                Refinement(ref coder, plane, raw);
                break;
            }

            default:
            {
                Cleanup(ref coder, plane, (style & JpxBlockStyle.SegmentationSymbols) != 0);
                break;
            }
        }
    }

    /// <summary>Removes the region-of-interest up-shift from the coefficients that carry it (equation D-1).</summary>
    /// <param name="shift">The shift.</param>
    private void ApplyRoi(int shift)
    {
        var threshold = 1 << shift;
        foreach (ref var value in _values.AsSpan(0, _length))
        {
            var magnitude = Math.Abs(value);
            if (magnitude < threshold)
            {
                continue;
            }

            magnitude >>= shift;
            value = value < 0 ? -magnitude : magnitude;
        }
    }

    /// <summary>Copies the coefficients into the tile-component, halved or scaled by half the step.</summary>
    /// <param name="block">The code-block.</param>
    /// <param name="band">The sub-band.</param>
    /// <param name="target">The tile-component.</param>
    private void Store(JpxCodeBlock block, in JpxBandLayout band, in JpxBlockTarget target)
    {
        var x0 = band.BufferX + block.Area.X0 - band.Area.X0;
        var y0 = band.BufferY + block.Area.Y0 - band.Area.Y0;
        var floats = MemoryMarshal.Cast<int, float>(target.Buffer.AsSpan());
        for (var y = 0; y < _height; y++)
        {
            var source = (((y / StripeRows) + 1) * _stripeStride) + StripeRows + (y % StripeRows);
            var row = ((y0 + y) * target.Stride) + x0;
            if (target.Reversible)
            {
                StoreReversible(target.Buffer.AsSpan(row, _width), source);
            }
            else
            {
                StoreIrreversible(floats.Slice(row, _width), source, band.StepSize);
            }
        }
    }

    /// <summary>Stores one row of 5/3 coefficients, halving with truncation toward zero.</summary>
    /// <param name="row">The destination row.</param>
    /// <param name="source">The first coefficient in the state buffer.</param>
    private void StoreReversible(Span<int> row, int source)
    {
        for (var x = 0; x < row.Length; x++)
        {
            row[x] = _values[source + (x * StripeRows)] / Scale;
        }
    }

    /// <summary>Stores one row of 9/7 coefficients scaled by half the step.</summary>
    /// <param name="row">The destination row.</param>
    /// <param name="source">The first coefficient in the state buffer.</param>
    /// <param name="step">Half the dequantization step.</param>
    private void StoreIrreversible(Span<float> row, int source, float step)
    {
        for (var x = 0; x < row.Length; x++)
        {
            row[x] = _values[source + (x * StripeRows)] * step;
        }
    }

    /// <summary>The position of the decoder in the pass sequence.</summary>
    /// <param name="Plane">The bit-plane plus one.</param>
    /// <param name="Pass">The pass within the bit-plane: 0 significance, 1 refinement, 2 cleanup.</param>
    private readonly record struct PassCursor(int Plane, int Pass);
}
