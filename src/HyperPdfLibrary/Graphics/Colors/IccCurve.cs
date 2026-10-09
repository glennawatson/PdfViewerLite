// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>An ICC tone reproduction curve (a 'curv' or 'para' tag) that maps an encoded value from 0 to 1 to a linear one.</summary>
[DebuggerDisplay("IccCurve: {_kind}")]
internal sealed class IccCurve
{
    /// <summary>The 'curv' tag type signature.</summary>
    private const uint CurveSignature = 0x63757276;

    /// <summary>The 'para' tag type signature.</summary>
    private const uint ParametricSignature = 0x70617261;

    /// <summary>The offset of the curve count or the parametric function type.</summary>
    private const int HeaderSize = 8;

    /// <summary>The offset of the table or parameters.</summary>
    private const int DataOffset = 12;

    /// <summary>The bytes in a 16-bit table entry.</summary>
    private const int EntrySize = 2;

    /// <summary>The bytes in an s15Fixed16 parameter.</summary>
    private const int ParameterSize = 4;

    /// <summary>The alignment of stored curves in bytes.</summary>
    private const int Alignment = 4;

    /// <summary>The most table entries read.</summary>
    private const int MaxEntries = 65_536;

    /// <summary>The scale of an s15Fixed16 number.</summary>
    private const float FixedScale = 65_536F;

    /// <summary>The offset from the last table entry to the start of the last interpolation segment.</summary>
    private const int SegmentCount = 2;

    /// <summary>The scale of a u8Fixed8 gamma.</summary>
    private const float GammaScale = 256F;

    /// <summary>The largest 16-bit table value.</summary>
    private const float TableMaximum = 65535F;

    /// <summary>The parametric parameter g.</summary>
    private const int ParamG = 0;

    /// <summary>The parametric parameter a.</summary>
    private const int ParamA = 1;

    /// <summary>The parametric parameter b.</summary>
    private const int ParamB = 2;

    /// <summary>The parametric parameter c.</summary>
    private const int ParamC = 3;

    /// <summary>The parametric parameter d.</summary>
    private const int ParamD = 4;

    /// <summary>The parametric parameter e.</summary>
    private const int ParamE = 5;

    /// <summary>The parametric parameter f.</summary>
    private const int ParamF = 6;

    /// <summary>The kind of curve.</summary>
    private readonly CurveKind _kind;

    /// <summary>The parametric function type, 0 to 4.</summary>
    private readonly ParametricType _function;

    /// <summary>The gamma of a gamma curve.</summary>
    private readonly float _gamma;

    /// <summary>The values of a table curve, from 0 to 1.</summary>
    private readonly float[] _table;

    /// <summary>The parameters of a parametric curve.</summary>
    private readonly float[] _parameters;

    /// <summary>Initializes a new instance of the <see cref="IccCurve"/> class.</summary>
    /// <param name="kind">The kind of curve.</param>
    /// <param name="function">The parametric function type.</param>
    /// <param name="gamma">The gamma.</param>
    /// <param name="table">The table values.</param>
    /// <param name="parameters">The parametric parameters.</param>
    private IccCurve(CurveKind kind, ParametricType function, float gamma, float[] table, float[] parameters)
    {
        _kind = kind;
        _function = function;
        _gamma = gamma;
        _table = table;
        _parameters = parameters;
    }

    /// <summary>The kinds of curve.</summary>
    private enum CurveKind
    {
        /// <summary>The identity curve.</summary>
        Identity = 0,

        /// <summary>A power curve.</summary>
        Gamma = 1,

        /// <summary>A sampled curve.</summary>
        Table = 2,

        /// <summary>A parametric curve.</summary>
        Parametric = 3,
    }

    /// <summary>The parametric function types of the ICC specification.</summary>
    private enum ParametricType
    {
        /// <summary>Y = X^g.</summary>
        Power = 0,

        /// <summary>Y = (aX + b)^g from X = -b/a, else 0.</summary>
        Break = 1,

        /// <summary>Y = (aX + b)^g + c from X = -b/a, else c.</summary>
        BreakOffset = 2,

        /// <summary>Y = (aX + b)^g from X = d, else cX.</summary>
        Linear = 3,

        /// <summary>Y = (aX + b)^g + e from X = d, else cX + f.</summary>
        LinearOffset = 4,
    }

    /// <summary>Gets a value indicating whether the curve is the identity.</summary>
    internal bool IsIdentity => _kind == CurveKind.Identity;

    /// <summary>Reads a curve tag.</summary>
    /// <param name="tag">The tag data, starting at its type signature.</param>
    /// <returns>The curve, or <see langword="null"/> when the tag is not a valid curve.</returns>
    internal static IccCurve? Parse(ReadOnlySpan<byte> tag) => tag.Length < DataOffset ? null : BinaryPrimitives.ReadUInt32BigEndian(tag) switch
    {
        CurveSignature => ParseCurve(tag),
        ParametricSignature => ParseParametric(tag),
        _ => null,
    };

    /// <summary>Creates a sampled curve whose values are spread evenly over the input range.</summary>
    /// <param name="values">The sampled values from 0 to 1; at least two.</param>
    /// <returns>The curve, or <see langword="null"/> when there are fewer than two values.</returns>
    internal static IccCurve? FromTable(float[] values) => values.Length < SegmentCount ? null : new(CurveKind.Table, 0, 1, values, []);

    /// <summary>Gets the bytes a curve tag occupies when stored back to back with others, padded to four bytes.</summary>
    /// <param name="tag">The data starting at the curve's type signature.</param>
    /// <returns>The padded length, or zero when the data is not a complete curve.</returns>
    internal static int Measure(ReadOnlySpan<byte> tag)
    {
        if (tag.Length < DataOffset)
        {
            return 0;
        }

        var length = BinaryPrimitives.ReadUInt32BigEndian(tag) switch
        {
            CurveSignature => DataOffset + ((long)BinaryPrimitives.ReadUInt32BigEndian(tag[HeaderSize..]) * EntrySize),
            ParametricSignature => ParameterCount((ParametricType)BinaryPrimitives.ReadUInt16BigEndian(tag[HeaderSize..])) is var count and > 0
                ? DataOffset + ((long)count * ParameterSize)
                : 0,
            _ => 0,
        };
        return length is 0 || length > tag.Length ? 0 : (int)((length + (Alignment - 1)) / Alignment * Alignment);
    }

    /// <summary>Evaluates the curve.</summary>
    /// <param name="value">The encoded value; clamped to 0..1.</param>
    /// <returns>The linear value from 0 to 1.</returns>
    internal float Evaluate(float value)
    {
        var x = Math.Clamp(value, 0, 1);
        var result = _kind switch
        {
            CurveKind.Gamma => MathF.Pow(x, _gamma),
            CurveKind.Table => EvaluateTable(x),
            CurveKind.Parametric => EvaluateParametric(x),
            _ => x,
        };
        return float.IsNaN(result) ? 0 : Math.Clamp(result, 0, 1);
    }

    /// <summary>Reads a 'curv' tag.</summary>
    /// <param name="tag">The tag data.</param>
    /// <returns>The curve, or <see langword="null"/> when truncated.</returns>
    private static IccCurve? ParseCurve(ReadOnlySpan<byte> tag)
    {
        var count = BinaryPrimitives.ReadUInt32BigEndian(tag[HeaderSize..]);
        if (count == 0)
        {
            return new(CurveKind.Identity, 0, 1, [], []);
        }

        if (count == 1)
        {
            return tag.Length < DataOffset + EntrySize ? null : new(CurveKind.Gamma, 0, BinaryPrimitives.ReadUInt16BigEndian(tag[DataOffset..]) / GammaScale, [], []);
        }

        if (count > MaxEntries || tag.Length < DataOffset + ((long)count * EntrySize))
        {
            return null;
        }

        var table = new float[count];
        for (var i = 0; i < table.Length; i++)
        {
            table[i] = BinaryPrimitives.ReadUInt16BigEndian(tag[(DataOffset + (i * EntrySize))..]) / TableMaximum;
        }

        return new(CurveKind.Table, 0, 1, table, []);
    }

    /// <summary>Reads a 'para' tag.</summary>
    /// <param name="tag">The tag data.</param>
    /// <returns>The curve, or <see langword="null"/> when invalid.</returns>
    private static IccCurve? ParseParametric(ReadOnlySpan<byte> tag)
    {
        var function = (ParametricType)BinaryPrimitives.ReadUInt16BigEndian(tag[HeaderSize..]);
        var count = ParameterCount(function);
        if (count == 0 || tag.Length < DataOffset + (count * ParameterSize))
        {
            return null;
        }

        var parameters = new float[ParamF + 1];
        for (var i = 0; i < count; i++)
        {
            parameters[i] = BinaryPrimitives.ReadInt32BigEndian(tag[(DataOffset + (i * ParameterSize))..]) / FixedScale;
        }

        return new(CurveKind.Parametric, function, 1, [], parameters);
    }

    /// <summary>Gets the number of parameters of a parametric function type.</summary>
    /// <param name="function">The function type.</param>
    /// <returns>The count, or zero for an unknown type.</returns>
    private static int ParameterCount(ParametricType function) => function switch
    {
        ParametricType.Power => ParamG + 1,
        ParametricType.Break => ParamB + 1,
        ParametricType.BreakOffset => ParamC + 1,
        ParametricType.Linear => ParamD + 1,
        ParametricType.LinearOffset => ParamF + 1,
        _ => 0,
    };

    /// <summary>Gets the input where function types 1 and 2 start their power segment, -b / a.</summary>
    /// <param name="p">The parameters.</param>
    /// <returns>The break point.</returns>
    private static float Break(float[] p) => p[ParamA] is 0 ? float.NegativeInfinity : -p[ParamB] / p[ParamA];

    /// <summary>Evaluates a table curve with linear interpolation.</summary>
    /// <param name="x">The input from 0 to 1.</param>
    /// <returns>The interpolated value.</returns>
    private float EvaluateTable(float x)
    {
        var position = x * (_table.Length - 1);
        var index = Math.Min((int)position, _table.Length - SegmentCount);
        return _table[index] + ((_table[index + 1] - _table[index]) * (position - index));
    }

    /// <summary>Evaluates a parametric curve.</summary>
    /// <param name="x">The input from 0 to 1.</param>
    /// <returns>The value.</returns>
    private float EvaluateParametric(float x)
    {
        var p = _parameters;
        var power = MathF.Pow(Math.Max((p[ParamA] * x) + p[ParamB], 0), p[ParamG]);
        var threshold = _function is ParametricType.Break or ParametricType.BreakOffset ? Break(p) : p[ParamD];
        return _function switch
        {
            ParametricType.Power => MathF.Pow(x, p[ParamG]),
            ParametricType.Break => x >= threshold ? power : 0,
            ParametricType.BreakOffset => x >= threshold ? power + p[ParamC] : p[ParamC],
            ParametricType.Linear => x >= threshold ? power : p[ParamC] * x,
            _ => x >= threshold ? power + p[ParamE] : (p[ParamC] * x) + p[ParamF],
        };
    }
}
