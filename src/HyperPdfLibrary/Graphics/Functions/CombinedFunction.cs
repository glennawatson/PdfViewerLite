// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>An array of one-input, one-output functions acting as one function with an output per member.</summary>
internal sealed class CombinedFunction : PdfFunction
{
    /// <summary>The unbounded domain of the combined function; each member clamps its own input.</summary>
    private static readonly float[] UnboundedDomain = [float.MinValue, float.MaxValue];

    /// <summary>The member functions, one per output.</summary>
    private readonly PdfFunction[] _functions;

    /// <summary>Initializes a new instance of the <see cref="CombinedFunction"/> class.</summary>
    /// <param name="functions">The member functions.</param>
    private CombinedFunction(PdfFunction[] functions)
        : base(UnboundedDomain, null, functions.Length) => _functions = functions;

    /// <summary>Parses an array of functions.</summary>
    /// <param name="array">The array.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The combined function, or <see langword="null"/> when any member is invalid.</returns>
    internal static PdfFunction? Parse(PdfArray array, int depth)
    {
        if (array.Count == 0 || array.Count > MaxComponents)
        {
            return null;
        }

        var functions = new PdfFunction[array.Count];
        for (var i = 0; i < functions.Length; i++)
        {
            var function = Parse(array.Get(i), depth + 1);
            if (function is null || function.InputCount != 1 || function.OutputCount != 1)
            {
                return null;
            }

            functions[i] = function;
        }

        return functions.Length == 1 ? functions[0] : new CombinedFunction(functions);
    }

    /// <inheritdoc/>
    private protected override void EvaluateCore(ReadOnlySpan<float> input, Span<float> output)
    {
        for (var i = 0; i < _functions.Length; i++)
        {
            _functions[i].Evaluate(input, output.Slice(i, 1));
        }
    }
}
