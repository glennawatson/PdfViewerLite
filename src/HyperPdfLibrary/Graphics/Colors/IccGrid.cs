// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// A table of sRGB values sampled from an ICC pipeline at evenly spaced device colours, and the per-byte tables that locate
/// 8-bit samples in it. The table stores the sRGB curve extended past 0 and 1 so that interpolation crosses the edge of the
/// sRGB gamut where the exact conversion does; the output clips to 0..1. When the pipeline has a look-up table, grid nodes
/// fall on its nodes, which reproduces its piecewise-linear shape between them. Instances are immutable.
/// </summary>
[DebuggerDisplay("IccGrid: {Clut.InputCount} inputs")]
internal sealed class IccGrid
{
    /// <summary>The number of values a sample byte takes.</summary>
    private const int SampleValues = PixelConverter.LookupSize;

    /// <summary>The channels of an RGB grid.</summary>
    private const int RgbChannels = 3;

    /// <summary>The node count below which nodes are computed on one thread.</summary>
    private const int ParallelThreshold = 4096;

    /// <summary>The fewest cells along each channel for each channel count; a single channel has a cell per byte step.</summary>
    private static readonly int[] MinCells = [0, 255, 32, 32, 24, 8, 6, 4, 3];

    /// <summary>The most cells along each channel for each channel count.</summary>
    private static readonly int[] MaxCells = [0, 255, 64, 32, 24, 8, 6, 4, 3];

    /// <summary>Initializes a new instance of the <see cref="IccGrid"/> class.</summary>
    /// <param name="clut">The table of sRGB values.</param>
    /// <param name="isIdentity">Whether the table maps each channel to itself.</param>
    private IccGrid(IccClut clut, bool isIdentity)
    {
        Clut = clut;
        IsIdentity = isIdentity;
        var channels = clut.InputCount;
        Offsets = new int[channels * SampleValues];
        Fractions = new float[SampleValues];
        for (var b = 0; b < SampleValues; b++)
        {
            for (var c = 0; c < channels; c++)
            {
                clut.Locate(c, b / (float)PixelConverter.MaxByte, out Offsets[(c * SampleValues) + b], out var fraction);
                Fractions[b] = fraction;
            }
        }
    }

    /// <summary>Gets the table of sRGB values.</summary>
    internal IccClut Clut { get; }

    /// <summary>Gets a value indicating whether the table maps each channel to itself.</summary>
    internal bool IsIdentity { get; }

    /// <summary>Gets the node offset of the cell holding each byte, for each channel in turn.</summary>
    internal int[] Offsets { get; }

    /// <summary>Gets the position within its cell of each byte; every channel has the same grid size, so one table serves all.</summary>
    internal float[] Fractions { get; }

    /// <summary>Evaluates a pipeline at every grid node.</summary>
    /// <param name="pipeline">The pipeline.</param>
    /// <param name="compensation">The black point compensation, or <see langword="null"/>.</param>
    /// <param name="labInput">Whether the device space is Lab.</param>
    /// <returns>The grid.</returns>
    internal static IccGrid Build(IccPipeline pipeline, BlackPointCompensation? compensation, bool labInput)
    {
        var channels = pipeline.Inputs;
        var size = CellsFor(channels, pipeline.SourceCells) + 1;
        var builder = new SliceBuilder(pipeline, compensation, size, channels, channels == RgbChannels && !labInput);
        if (builder.Nodes.Length < ParallelThreshold)
        {
            for (var first = 0; first < size; first++)
            {
                builder.BuildSlice(first);
            }
        }
        else
        {
            _ = Parallel.For(0, size, builder.BuildSlice);
        }

        Span<int> sizes = stackalloc int[IccClut.MaxInputs];
        sizes = sizes[..channels];
        sizes.Fill(size);
        return new(new(sizes, builder.Nodes), builder.IsIdentity);
    }

    /// <summary>Chooses the cells along each channel: enough for accuracy, and a multiple of the source table's cells when possible.</summary>
    /// <param name="channels">The number of channels.</param>
    /// <param name="sourceCells">The cells along each channel of the profile's own table, or zero when it has none.</param>
    /// <returns>The number of cells.</returns>
    private static int CellsFor(int channels, int sourceCells)
    {
        var minimum = MinCells[channels];
        if (sourceCells < 1)
        {
            return minimum;
        }

        var cells = sourceCells * ((minimum + sourceCells - 1) / sourceCells);
        return cells <= MaxCells[channels] ? cells : MaxCells[channels];
    }

    /// <summary>Computes the nodes of one slice of the grid, the nodes that share a first channel position.</summary>
    private sealed class SliceBuilder
    {
        /// <summary>The largest difference from the input, in output units, for a grid still counted as the identity.</summary>
        private const float IdentityTolerance = 1.5F / PixelConverter.MaxByte;

        /// <summary>How far past 0 and 1 a node may store an encoded value.</summary>
        private const float ExtentLimit = 1F;

        /// <summary>The pipeline to sample.</summary>
        private readonly IccPipeline _pipeline;

        /// <summary>The black point compensation, or <see langword="null"/>.</summary>
        private readonly BlackPointCompensation? _compensation;

        /// <summary>The nodes along each channel.</summary>
        private readonly int _size;

        /// <summary>The number of channels.</summary>
        private readonly int _channels;

        /// <summary>Whether any node has differed from the identity; 1 when one has.</summary>
        private int _differs;

        /// <summary>Initializes a new instance of the <see cref="SliceBuilder"/> class.</summary>
        /// <param name="pipeline">The pipeline to sample.</param>
        /// <param name="compensation">The black point compensation, or <see langword="null"/>.</param>
        /// <param name="size">The nodes along each channel.</param>
        /// <param name="channels">The number of channels.</param>
        /// <param name="checkIdentity">Whether to check that the grid maps each channel to itself.</param>
        internal SliceBuilder(IccPipeline pipeline, BlackPointCompensation? compensation, int size, int channels, bool checkIdentity)
        {
            _pipeline = pipeline;
            _compensation = compensation;
            _size = size;
            _channels = channels;
            _differs = checkIdentity ? 0 : 1;
            var total = 1;
            for (var c = 0; c < channels; c++)
            {
                total *= size;
            }

            Nodes = new Vector128<float>[total];
        }

        /// <summary>Gets the nodes, filled by <see cref="BuildSlice"/>.</summary>
        internal Vector128<float>[] Nodes { get; }

        /// <summary>Gets a value indicating whether every node held the colour at its own position.</summary>
        internal bool IsIdentity => Volatile.Read(ref _differs) == 0;

        /// <summary>Computes the nodes whose first channel is at a position.</summary>
        /// <param name="first">The node index along the first channel.</param>
        internal void BuildSlice(int first)
        {
            Span<int> index = stackalloc int[IccClut.MaxInputs];
            index = index[.._channels];
            index.Clear();
            index[0] = first;
            Span<float> input = stackalloc float[IccClut.MaxInputs];
            input = input[.._channels];
            var perSlice = Nodes.Length / _size;
            var start = first * perSlice;
            var differs = false;
            for (var k = 0; k < perSlice; k++)
            {
                for (var c = 0; c < _channels; c++)
                {
                    input[c] = index[c] / (float)(_size - 1);
                }

                var node = ToSrgb(_pipeline.ToXyz(input), _compensation);
                Nodes[start + k] = node;
                differs = differs || (Volatile.Read(ref _differs) == 0 && !IsNear(node, input));
                Advance(index[1..], _size);
            }

            if (differs)
            {
                Volatile.Write(ref _differs, 1);
            }
        }

        /// <summary>Encodes a linear value with the extended sRGB curve.</summary>
        /// <param name="linear">The linear value.</param>
        /// <returns>The encoded value, which may be outside 0..1 by up to <see cref="ExtentLimit"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Encode(float linear) => Math.Clamp(SrgbTransfer.EncodeExtended(linear), -ExtentLimit, 1F + ExtentLimit);

        /// <summary>Converts D50 XYZ to an sRGB node.</summary>
        /// <param name="xyz">The colour.</param>
        /// <param name="compensation">The black point compensation, or <see langword="null"/>.</param>
        /// <returns>The sRGB values in the first three lanes.</returns>
        private static Vector128<float> ToSrgb(Float3 xyz, BlackPointCompensation? compensation)
        {
            var adjusted = compensation is { } map ? map.Apply(xyz) : xyz;
            var linear = IccProfile.XyzToSrgb.Transform(adjusted.X, adjusted.Y, adjusted.Z);
            return Vector128.Create(Encode(linear.X), Encode(linear.Y), Encode(linear.Z), 0F);
        }

        /// <summary>Determines whether a node holds the colour at its own position.</summary>
        /// <param name="node">The node.</param>
        /// <param name="input">The position of the node, three channels.</param>
        /// <returns><see langword="true"/> when the node is within the tolerance of the position.</returns>
        private static bool IsNear(Vector128<float> node, ReadOnlySpan<float> input) =>
            MathF.Abs(node.GetElement(0) - input[0]) <= IdentityTolerance
            && MathF.Abs(node.GetElement(1) - input[1]) <= IdentityTolerance
            && MathF.Abs(node.GetElement(RgbChannels - 1) - input[RgbChannels - 1]) <= IdentityTolerance;

        /// <summary>Moves a grid index to the next node, with the last channel varying fastest.</summary>
        /// <param name="index">The index along each channel to advance.</param>
        /// <param name="size">The nodes along each channel.</param>
        private static void Advance(Span<int> index, int size)
        {
            for (var c = index.Length - 1; c >= 0; c--)
            {
                index[c]++;
                if (index[c] < size)
                {
                    return;
                }

                index[c] = 0;
            }
        }
    }
}
