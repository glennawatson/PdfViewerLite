// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// The device to connection space pipeline of an AToB tag: input curves, a look-up table, optional matrix curves and
/// matrix, and output curves. It evaluates one colour at a time and allocates nothing; it builds the faster
/// <see cref="IccLutTransform"/> grid once.
/// </summary>
[DebuggerDisplay("IccPipeline: {Inputs} inputs, {_encoding}")]
internal sealed class IccPipeline
{
    /// <summary>The numbers in a three by four matrix, which is three rows of three coefficients and an offset column.</summary>
    internal const int MatrixSize = 12;

    /// <summary>The coefficients per matrix row, not counting the offset.</summary>
    private const int MatrixRow = 3;

    /// <summary>The offset of the first additive term in the matrix.</summary>
    private const int MatrixOffset = 9;

    /// <summary>The curves applied to each input channel, or <see langword="null"/>.</summary>
    private readonly IccCurve[]? _inputCurves;

    /// <summary>The look-up table, or <see langword="null"/> for a pipeline of curves and a matrix only.</summary>
    private readonly IccClut? _clut;

    /// <summary>The curves applied before the matrix, or <see langword="null"/>.</summary>
    private readonly IccCurve[]? _matrixCurves;

    /// <summary>The three by four matrix, or <see langword="null"/>.</summary>
    private readonly float[]? _matrix;

    /// <summary>The curves applied to each output channel, or <see langword="null"/>.</summary>
    private readonly IccCurve[]? _outputCurves;

    /// <summary>How the output is stored in the connection space.</summary>
    private readonly IccPcsEncoding _encoding;

    /// <summary>Initializes a new instance of the <see cref="IccPipeline"/> class.</summary>
    /// <param name="inputs">The number of input channels.</param>
    /// <param name="inputCurves">The curves applied to each input channel, or <see langword="null"/>.</param>
    /// <param name="clut">The look-up table, or <see langword="null"/>.</param>
    /// <param name="matrixCurves">The three curves applied before the matrix, or <see langword="null"/>.</param>
    /// <param name="matrix">The three by four matrix, row by row with the offsets last, or <see langword="null"/>.</param>
    /// <param name="outputCurves">The three output curves, or <see langword="null"/>.</param>
    /// <param name="encoding">How the output is stored in the connection space.</param>
    internal IccPipeline(
        int inputs,
        IccCurve[]? inputCurves,
        IccClut? clut,
        IccCurve[]? matrixCurves,
        float[]? matrix,
        IccCurve[]? outputCurves,
        IccPcsEncoding encoding)
    {
        Inputs = inputs;
        _inputCurves = inputCurves;
        _clut = clut;
        _matrixCurves = matrixCurves;
        _matrix = matrix;
        _outputCurves = outputCurves;
        _encoding = encoding;
    }

    /// <summary>Gets the number of input channels.</summary>
    internal int Inputs { get; }

    /// <summary>Gets the cells along the widest channel of the look-up table, or zero when the pipeline has none.</summary>
    internal int SourceCells => _clut?.MaxCells ?? 0;

    /// <summary>Evaluates the pipeline.</summary>
    /// <param name="input">One value per input channel, each from 0 to 1.</param>
    /// <returns>The colour in D50 XYZ.</returns>
    internal Float3 ToXyz(ReadOnlySpan<float> input)
    {
        Span<float> staged = stackalloc float[IccClut.MaxInputs];
        staged = staged[..Inputs];
        input[..Inputs].CopyTo(staged);
        Apply(_inputCurves, staged);
        var color = _clut is null ? new Float3(staged[0], staged[1], staged[^1]) : ToFloat3(_clut.Evaluate(staged));
        color = ApplyMatrix(ApplyCurves(color, _matrixCurves));
        color = ApplyCurves(color, _outputCurves);
        return IccPcs.ToXyz(_encoding, color.X, color.Y, color.Z);
    }

    /// <summary>Reads the first three lanes of a vector.</summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The lanes.</returns>
    private static Float3 ToFloat3(Vector128<float> vector) => new(vector.GetElement(0), vector.GetElement(1), vector.GetElement(MatrixRow - 1));

    /// <summary>Applies one curve to each value.</summary>
    /// <param name="curves">The curves, or <see langword="null"/> to leave the values alone.</param>
    /// <param name="values">The values, changed in place.</param>
    private static void Apply(IccCurve[]? curves, Span<float> values)
    {
        if (curves is null)
        {
            return;
        }

        for (var i = 0; i < values.Length; i++)
        {
            values[i] = curves[i].Evaluate(values[i]);
        }
    }

    /// <summary>Applies three curves to a colour.</summary>
    /// <param name="color">The colour.</param>
    /// <param name="curves">The three curves, or <see langword="null"/> to leave the colour alone.</param>
    /// <returns>The curved colour.</returns>
    private static Float3 ApplyCurves(Float3 color, IccCurve[]? curves) =>
        curves is null ? color : new(curves[0].Evaluate(color.X), curves[1].Evaluate(color.Y), curves[MatrixRow - 1].Evaluate(color.Z));

    /// <summary>Evaluates one row of the matrix.</summary>
    /// <param name="m">The matrix.</param>
    /// <param name="start">The index of the row's first coefficient.</param>
    /// <param name="color">The colour.</param>
    /// <returns>The row's coefficients times the colour, plus the row's offset.</returns>
    private static float Row(float[] m, int start, Float3 color) =>
        (m[start] * color.X) + (m[start + 1] * color.Y) + (m[start + MatrixRow - 1] * color.Z) + m[MatrixOffset + (start / MatrixRow)];

    /// <summary>Applies the matrix and its offsets.</summary>
    /// <param name="color">The colour.</param>
    /// <returns>The transformed colour.</returns>
    private Float3 ApplyMatrix(Float3 color)
    {
        var m = _matrix;
        return m is null
            ? color
            : new(Row(m, 0, color), Row(m, MatrixRow, color), Row(m, MatrixRow + MatrixRow, color));
    }
}
