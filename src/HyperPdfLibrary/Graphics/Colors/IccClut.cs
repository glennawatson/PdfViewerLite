// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// A colour look-up table with up to <see cref="MaxInputs"/> input channels and three output channels, each node held in
/// the first three lanes of a <see cref="Vector128{T}"/>. The last three dimensions interpolate tetrahedrally and earlier
/// dimensions interpolate linearly. Tables with fewer than three inputs gain extra two-node dimensions so one code path
/// serves all. Instances are immutable and safe to share between threads; interpolation does not allocate.
/// </summary>
[DebuggerDisplay("IccClut: {InputCount} inputs, {_nodes.Length} nodes")]
internal sealed class IccClut
{
    /// <summary>The most input channels a table may have.</summary>
    internal const int MaxInputs = 8;

    /// <summary>The fewest dimensions the interpolator works on.</summary>
    private const int MinDimensions = 3;

    /// <summary>The nodes per dimension of a padding dimension.</summary>
    private const int PaddingSize = 2;

    /// <summary>The distance from the size of a dimension back to the index of its last cell.</summary>
    private const int LastCellBack = 2;

    /// <summary>The nodes of every dimension, in table order.</summary>
    private readonly Vector128<float>[] _nodes;

    /// <summary>The nodes along each dimension, padded to at least three dimensions.</summary>
    private readonly int[] _sizes;

    /// <summary>The distance in nodes between neighbours along each dimension.</summary>
    private readonly int[] _strides;

    /// <summary>The index of the first dimension that interpolates tetrahedrally.</summary>
    private readonly int _tetrahedralLevel;

    /// <summary>Initializes a new instance of the <see cref="IccClut"/> class.</summary>
    /// <param name="sizes">The nodes along each input channel, each at least two. The first channel varies slowest.</param>
    /// <param name="nodes">The nodes, with the first channel varying slowest; one entry per combination of channel nodes.</param>
    internal IccClut(ReadOnlySpan<int> sizes, Vector128<float>[] nodes)
    {
        InputCount = sizes.Length;
        var padding = Math.Max(0, MinDimensions - sizes.Length);
        _sizes = new int[sizes.Length + padding];
        sizes.CopyTo(_sizes);
        _sizes.AsSpan(sizes.Length).Fill(PaddingSize);
        _nodes = padding == 0 ? nodes : Replicate(nodes, 1 << padding);
        _strides = new int[_sizes.Length];
        var stride = 1;
        for (var i = _sizes.Length - 1; i >= 0; i--)
        {
            _strides[i] = stride;
            stride *= _sizes[i];
        }

        _tetrahedralLevel = _sizes.Length - MinDimensions;
    }

    /// <summary>Gets the number of input channels the table was built with.</summary>
    internal int InputCount { get; }

    /// <summary>Gets the cells along the widest input channel, which is one less than its nodes.</summary>
    internal int MaxCells
    {
        get
        {
            var widest = 0;
            for (var i = 0; i < InputCount; i++)
            {
                widest = Math.Max(widest, _sizes[i]);
            }

            return widest - 1;
        }
    }

    /// <summary>Gets the number of dimensions, which is at least three.</summary>
    internal int Dimensions => _sizes.Length;

    /// <summary>Gets the node at an index; used to read the table back.</summary>
    /// <param name="index">The node index.</param>
    /// <returns>The node.</returns>
    internal Vector128<float> this[int index] => _nodes[index];

    /// <summary>Finds the cell holding a value along one dimension.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="value">The value; clamped to 0..1.</param>
    /// <param name="offset">Receives the node offset of the cell's lower corner along this dimension.</param>
    /// <param name="fraction">Receives the position within the cell, from 0 to 1.</param>
    internal void Locate(int dimension, float value, out int offset, out float fraction)
    {
        var size = _sizes[dimension];
        var position = Math.Clamp(float.IsNaN(value) ? 0 : value, 0, 1) * (size - 1);
        var cell = Math.Min((int)position, size - LastCellBack);
        offset = cell * _strides[dimension];
        fraction = position - cell;
    }

    /// <summary>Gets the node offset of a cell along a dimension, for tables of equal-sized dimensions.</summary>
    /// <param name="dimension">The dimension.</param>
    /// <param name="cell">The cell index along the dimension.</param>
    /// <returns>The offset in nodes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int OffsetOf(int dimension, int cell) => cell * _strides[dimension];

    /// <summary>Evaluates the table.</summary>
    /// <param name="input">One value per input channel, each from 0 to 1.</param>
    /// <returns>The interpolated output in the first three lanes.</returns>
    internal Vector128<float> Evaluate(ReadOnlySpan<float> input)
    {
        Span<int> offsets = stackalloc int[MaxInputs + MinDimensions];
        Span<float> fractions = stackalloc float[MaxInputs + MinDimensions];
        offsets = offsets[.._sizes.Length];
        fractions = fractions[.._sizes.Length];
        for (var d = 0; d < _sizes.Length; d++)
        {
            Locate(d, d < InputCount ? input[d] : 0, out offsets[d], out fractions[d]);
        }

        return Interpolate(offsets, fractions);
    }

    /// <summary>Interpolates at a position given as per-dimension cells and fractions.</summary>
    /// <param name="offsets">The node offset of the lower corner along each dimension; one entry per dimension.</param>
    /// <param name="fractions">The position within the cell along each dimension; one entry per dimension.</param>
    /// <returns>The interpolated output in the first three lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Vector128<float> Interpolate(ReadOnlySpan<int> offsets, ReadOnlySpan<float> fractions) => Descend(0, 0, offsets, fractions);

    /// <summary>Interpolates in a table of three dimensions.</summary>
    /// <param name="node">The index of the cell's lower corner.</param>
    /// <param name="f0">The fraction along the first dimension.</param>
    /// <param name="f1">The fraction along the second dimension.</param>
    /// <param name="f2">The fraction along the third dimension.</param>
    /// <returns>The interpolated output in the first three lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Vector128<float> Interpolate3(int node, float f0, float f1, float f2) => Tetrahedral(node, new(f0, f1, f2));

    /// <summary>Interpolates in a table of four dimensions.</summary>
    /// <param name="node">The index of the cell's lower corner.</param>
    /// <param name="f0">The fraction along the first dimension.</param>
    /// <param name="f1">The fraction along the second dimension.</param>
    /// <param name="f2">The fraction along the third dimension.</param>
    /// <param name="f3">The fraction along the fourth dimension.</param>
    /// <returns>The interpolated output in the first three lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Vector128<float> Interpolate4(int node, float f0, float f1, float f2, float f3)
    {
        var rest = new Axes(f1, f2, f3);
        var low = Tetrahedral(node, rest);
        if (f0 == 0)
        {
            return low;
        }

        var high = Tetrahedral(node + _strides[0], rest);
        return low + ((high - low) * Vector128.Create(f0));
    }

    /// <summary>Copies every node a number of times in a row.</summary>
    /// <param name="nodes">The nodes.</param>
    /// <param name="copies">The copies of each node.</param>
    /// <returns>The widened table.</returns>
    private static Vector128<float>[] Replicate(Vector128<float>[] nodes, int copies)
    {
        var widened = new Vector128<float>[nodes.Length * copies];
        for (var i = 0; i < nodes.Length; i++)
        {
            Array.Fill(widened, nodes[i], i * copies, copies);
        }

        return widened;
    }

    /// <summary>Orders the three tetrahedral axes by weight.</summary>
    /// <param name="x">The step in nodes along the first axis.</param>
    /// <param name="y">The step in nodes along the second axis.</param>
    /// <param name="z">The step in nodes along the third axis.</param>
    /// <param name="f">The fractions along the three axes.</param>
    /// <returns>The path from the lower corner to the opposite corner.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TetrahedralPath Order(int x, int y, int z, Axes f)
    {
        if (f.X >= f.Y)
        {
            if (f.Y >= f.Z)
            {
                return new(x, x + y, f.X, f.Y, f.Z);
            }

            return f.X >= f.Z ? new(x, x + z, f.X, f.Z, f.Y) : new(z, z + x, f.Z, f.X, f.Y);
        }

        if (f.X >= f.Z)
        {
            return new(y, y + x, f.Y, f.X, f.Z);
        }

        return f.Y >= f.Z ? new(y, y + z, f.Y, f.Z, f.X) : new(z, z + y, f.Z, f.Y, f.X);
    }

    /// <summary>Interpolates the dimensions from a level down.</summary>
    /// <param name="level">The first dimension still to interpolate.</param>
    /// <param name="node">The sum of the lower-corner offsets of the dimensions already handled, with their chosen corners.</param>
    /// <param name="offsets">The lower-corner offsets.</param>
    /// <param name="fractions">The fractions.</param>
    /// <returns>The interpolated output.</returns>
    private Vector128<float> Descend(int level, int node, ReadOnlySpan<int> offsets, ReadOnlySpan<float> fractions)
    {
        if (level == _tetrahedralLevel)
        {
            var last = _sizes.Length - 1;
            return Tetrahedral(
                node + offsets[level] + offsets[level + 1] + offsets[last],
                new(fractions[level], fractions[level + 1], fractions[last]));
        }

        var lower = node + offsets[level];
        var low = Descend(level + 1, lower, offsets, fractions);
        var fraction = fractions[level];
        if (fraction == 0)
        {
            return low;
        }

        var high = Descend(level + 1, lower + _strides[level], offsets, fractions);
        return low + ((high - low) * Vector128.Create(fraction));
    }

    /// <summary>Interpolates inside one cell of the last three dimensions along the tetrahedron holding the point.</summary>
    /// <param name="node">The index of the cell's lower corner.</param>
    /// <param name="f">The fractions along the three dimensions.</param>
    /// <returns>The interpolated output.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector128<float> Tetrahedral(int node, Axes f)
    {
        // Every node index comes from Locate, which keeps each cell and its far corner inside the table.
        ref var nodes = ref MemoryMarshal.GetArrayDataReference(_nodes);
        var corner = Unsafe.Add(ref nodes, node);
        if (f.X == 0 && f.Y == 0 && f.Z == 0)
        {
            return corner;
        }

        var x = _strides[_tetrahedralLevel];
        var y = _strides[_tetrahedralLevel + 1];
        var z = _strides[^1];
        var path = Order(x, y, z, f);
        var first = Unsafe.Add(ref nodes, node + path.First);
        var second = Unsafe.Add(ref nodes, node + path.Second);
        var far = Unsafe.Add(ref nodes, node + x + y + z);
        return corner
            + ((first - corner) * Vector128.Create(path.FirstWeight))
            + ((second - first) * Vector128.Create(path.SecondWeight))
            + ((far - second) * Vector128.Create(path.ThirdWeight));
    }

    /// <summary>Three values along the axes of a cell.</summary>
    /// <param name="X">The first axis.</param>
    /// <param name="Y">The second axis.</param>
    /// <param name="Z">The third axis.</param>
    private readonly record struct Axes(float X, float Y, float Z);

    /// <summary>The route from a cell's lower corner through two corners to the opposite corner, and the weight of each leg.</summary>
    /// <param name="First">The node offset of the first corner.</param>
    /// <param name="Second">The node offset of the second corner.</param>
    /// <param name="FirstWeight">The weight of the first leg.</param>
    /// <param name="SecondWeight">The weight of the second leg.</param>
    /// <param name="ThirdWeight">The weight of the third leg.</param>
    private readonly record struct TetrahedralPath(int First, int Second, float FirstWeight, float SecondWeight, float ThirdWeight);
}
