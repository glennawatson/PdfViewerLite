// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>A type 3 stitching function: one-input functions joined over subdomains of the domain.</summary>
internal sealed class StitchingFunction : PdfFunction
{
    /// <summary>The most functions stitched.</summary>
    private const int MaxFunctions = 256;

    /// <summary>The joined functions.</summary>
    private readonly PdfFunction[] _functions;

    /// <summary>The subdomain boundaries, one fewer than the functions.</summary>
    private readonly float[] _bounds;

    /// <summary>The interval each subdomain maps to, as pairs.</summary>
    private readonly float[] _encode;

    /// <summary>The low end of the domain.</summary>
    private readonly float _domainMin;

    /// <summary>The high end of the domain.</summary>
    private readonly float _domainMax;

    /// <summary>Initializes a new instance of the <see cref="StitchingFunction"/> class.</summary>
    /// <param name="domain">The input domain.</param>
    /// <param name="range">The output range, or <see langword="null"/>.</param>
    /// <param name="functions">The joined functions.</param>
    /// <param name="bounds">The subdomain boundaries.</param>
    /// <param name="encode">The subdomain mappings.</param>
    private StitchingFunction(float[] domain, float[]? range, PdfFunction[] functions, float[] bounds, float[] encode)
        : base(domain, range, functions[0].OutputCount)
    {
        _functions = functions;
        _bounds = bounds;
        _encode = encode;
        _domainMin = domain[0];
        _domainMax = domain[1];
    }

    /// <summary>Parses a stitching function.</summary>
    /// <param name="dictionary">The function dictionary.</param>
    /// <param name="domain">The input domain.</param>
    /// <param name="range">The output range, or <see langword="null"/>.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The function, or <see langword="null"/> when invalid.</returns>
    internal static StitchingFunction? Parse(PdfDictionary dictionary, float[] domain, float[]? range, int depth)
    {
        var array = dictionary.GetArray(KnownName.Functions);
        if (domain.Length != FunctionReader.PairSize || array is null || array.Count == 0 || array.Count > MaxFunctions)
        {
            return null;
        }

        var functions = ParseFunctions(array, depth);
        var bounds = FunctionReader.ReadExact(dictionary.GetArray(KnownName.Bounds), array.Count - 1, []);
        var encode = FunctionReader.ReadNumbers(dictionary.GetArray(KnownName.Encode));
        if (functions is null || bounds is null || encode is null || bounds.Length != array.Count - 1)
        {
            return null;
        }

        return IsValid(functions, bounds, encode, domain, range) ? new(domain, range, functions, bounds, encode) : null;
    }

    /// <inheritdoc/>
    private protected override void EvaluateCore(ReadOnlySpan<float> input, Span<float> output)
    {
        var x = input[0];
        var index = 0;
        while (index < _bounds.Length && x >= _bounds[index])
        {
            index++;
        }

        var low = index == 0 ? _domainMin : _bounds[index - 1];
        var high = index == _bounds.Length ? _domainMax : _bounds[index];
        var t = Interpolate(x, low, high, _encode[FunctionReader.PairSize * index], _encode[(FunctionReader.PairSize * index) + 1]);
        _functions[Math.Min(index, _functions.Length - 1)].Evaluate([t], output);
    }

    /// <summary>Parses the joined functions, which must all take one input and give the same number of outputs.</summary>
    /// <param name="array">The /Functions array.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The functions, or <see langword="null"/> when any is invalid.</returns>
    private static PdfFunction[]? ParseFunctions(PdfArray array, int depth)
    {
        var functions = new PdfFunction[array.Count];
        for (var i = 0; i < functions.Length; i++)
        {
            var function = Parse(array.Get(i), depth + 1);
            if (function is null || function.InputCount != 1 || function.OutputCount != (i == 0 ? function.OutputCount : functions[0].OutputCount))
            {
                return null;
            }

            functions[i] = function;
        }

        return functions;
    }

    /// <summary>Checks that the parts of a stitching function fit together.</summary>
    /// <param name="functions">The joined functions.</param>
    /// <param name="bounds">The bounds.</param>
    /// <param name="encode">The subdomain mappings.</param>
    /// <param name="domain">The domain.</param>
    /// <param name="range">The range, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    private static bool IsValid(PdfFunction[] functions, float[] bounds, float[] encode, float[] domain, float[]? range) =>
        encode.Length >= FunctionReader.PairSize * functions.Length
        && AreBoundsValid(bounds, domain)
        && (range is null || range.Length == FunctionReader.PairSize * functions[0].OutputCount);

    /// <summary>Checks that the bounds rise and lie inside the domain.</summary>
    /// <param name="bounds">The bounds.</param>
    /// <param name="domain">The domain.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    private static bool AreBoundsValid(float[] bounds, float[] domain)
    {
        var previous = domain[0];
        foreach (var bound in bounds)
        {
            if (!(bound >= previous) || bound > domain[1])
            {
                return false;
            }

            previous = bound;
        }

        return true;
    }
}
