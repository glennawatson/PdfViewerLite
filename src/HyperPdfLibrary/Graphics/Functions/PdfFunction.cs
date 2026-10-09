// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>
/// A PDF function (PDF 32000 section 7.10) that maps m inputs to n outputs. Parsed functions are immutable, so one instance
/// can be evaluated from many threads at once. Evaluation does not allocate.
/// </summary>
[DebuggerDisplay("PdfFunction: {InputCount} in, {OutputCount} out")]
public abstract class PdfFunction
{
    /// <summary>The most inputs or outputs a function may have.</summary>
    internal const int MaxComponents = 32;

    /// <summary>The deepest nesting of stitching and array functions parsed.</summary>
    private const int MaxDepth = 8;

    /// <summary>The input domain as min/max pairs.</summary>
    private readonly float[] _domain;

    /// <summary>The output range as min/max pairs, or <see langword="null"/> when outputs are not clamped.</summary>
    private readonly float[]? _range;

    /// <summary>Initializes a new instance of the <see cref="PdfFunction"/> class.</summary>
    /// <param name="domain">The input domain as min/max pairs.</param>
    /// <param name="range">The output range as min/max pairs, or <see langword="null"/>.</param>
    /// <param name="outputCount">The number of outputs.</param>
    private protected PdfFunction(float[] domain, float[]? range, int outputCount)
    {
        _domain = domain;
        _range = range;
        InputCount = domain.Length / FunctionReader.PairSize;
        OutputCount = outputCount;
    }

    /// <summary>Gets the number of inputs.</summary>
    public int InputCount { get; }

    /// <summary>Gets the number of outputs.</summary>
    public int OutputCount { get; }

    /// <summary>Parses a function from a function dictionary or stream, or an array of one-input, one-output functions.</summary>
    /// <param name="value">The function value, already resolved.</param>
    /// <returns>The function, or <see langword="null"/> when the value is not a valid function.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFunction? Parse(PdfValue value) => Parse(value, 0);

    /// <summary>Evaluates the function. Inputs are clamped to the domain and outputs to the range.</summary>
    /// <param name="input">The inputs; at least <see cref="InputCount"/> values.</param>
    /// <param name="output">Receives the outputs; at least <see cref="OutputCount"/> values.</param>
    /// <exception cref="ArgumentException">A span is shorter than the function needs.</exception>
    [SkipLocalsInit]
    public void Evaluate(ReadOnlySpan<float> input, Span<float> output)
    {
        if (input.Length < InputCount || output.Length < OutputCount)
        {
            throw new ArgumentException("The input or output span is shorter than the function needs.", nameof(input));
        }

        Span<float> clamped = stackalloc float[MaxComponents];
        clamped = clamped[..InputCount];
        for (var i = 0; i < clamped.Length; i++)
        {
            clamped[i] = Clamp(input[i], _domain[FunctionReader.PairSize * i], _domain[(FunctionReader.PairSize * i) + 1]);
        }

        var result = output[..OutputCount];
        EvaluateCore(clamped, result);
        if (_range is null)
        {
            return;
        }

        for (var i = 0; i < result.Length; i++)
        {
            result[i] = Clamp(result[i], _range[FunctionReader.PairSize * i], _range[(FunctionReader.PairSize * i) + 1]);
        }
    }

    /// <summary>Parses a function with a nesting depth.</summary>
    /// <param name="value">The function value.</param>
    /// <param name="depth">The current nesting depth.</param>
    /// <returns>The function, or <see langword="null"/>.</returns>
    internal static PdfFunction? Parse(PdfValue value, int depth)
    {
        if (depth > MaxDepth)
        {
            return null;
        }

        if (value.AsArray() is { } array)
        {
            return CombinedFunction.Parse(array, depth);
        }

        try
        {
            return ParseDictionary(value, depth);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    /// <summary>Clamps a value to an interval, mapping NaN to the low end.</summary>
    /// <param name="value">The value.</param>
    /// <param name="low">The low end.</param>
    /// <param name="high">The high end.</param>
    /// <returns>The clamped value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Clamp(float value, float low, float high)
    {
        if (value > high)
        {
            return high;
        }

        return value >= low ? value : low;
    }

    /// <summary>Maps a value linearly from one interval to another.</summary>
    /// <param name="x">The value.</param>
    /// <param name="fromLow">The source low end.</param>
    /// <param name="fromHigh">The source high end.</param>
    /// <param name="toLow">The target low end.</param>
    /// <param name="toHigh">The target high end.</param>
    /// <returns>The mapped value; the target low end when the source interval is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Interpolate(float x, float fromLow, float fromHigh, float toLow, float toHigh)
    {
        var span = fromHigh - fromLow;
        return span is 0 ? toLow : toLow + ((x - fromLow) * (toHigh - toLow) / span);
    }

    /// <summary>Computes the outputs from inputs already clamped to the domain.</summary>
    /// <param name="input">The clamped inputs.</param>
    /// <param name="output">Receives exactly <see cref="OutputCount"/> outputs.</param>
    private protected abstract void EvaluateCore(ReadOnlySpan<float> input, Span<float> output);

    /// <summary>Parses a function dictionary or stream.</summary>
    /// <param name="value">The function value.</param>
    /// <param name="depth">The current nesting depth.</param>
    /// <returns>The function, or <see langword="null"/>.</returns>
    private static PdfFunction? ParseDictionary(PdfValue value, int depth)
    {
        var dictionary = value.AsDictionary();
        var domain = FunctionReader.ReadIntervals(dictionary?.GetArray(KnownName.Domain));
        if (dictionary is null || domain is null || domain.Length > FunctionReader.PairSize * MaxComponents)
        {
            return null;
        }

        var range = FunctionReader.ReadIntervals(dictionary.GetArray(KnownName.Range));
        return range?.Length > FunctionReader.PairSize * MaxComponents ? null : Create(value, dictionary, domain, range, depth);
    }

    /// <summary>Creates the function of the dictionary's /FunctionType.</summary>
    /// <param name="value">The function value.</param>
    /// <param name="dictionary">The function dictionary.</param>
    /// <param name="domain">The input domain.</param>
    /// <param name="range">The output range, or <see langword="null"/>.</param>
    /// <param name="depth">The current nesting depth.</param>
    /// <returns>The function, or <see langword="null"/>.</returns>
    private static PdfFunction? Create(PdfValue value, PdfDictionary dictionary, float[] domain, float[]? range, int depth) =>
        dictionary.GetInt32(KnownName.FunctionType, -1) switch
        {
            FunctionReader.SampledType => SampledFunction.Parse(value.AsStream(), domain, range),
            FunctionReader.ExponentialType => ExponentialFunction.Parse(dictionary, domain, range),
            FunctionReader.StitchingType => StitchingFunction.Parse(dictionary, domain, range, depth),
            FunctionReader.PostScriptType => PostScriptFunction.Parse(value.AsStream(), domain, range),
            _ => null,
        };
}
