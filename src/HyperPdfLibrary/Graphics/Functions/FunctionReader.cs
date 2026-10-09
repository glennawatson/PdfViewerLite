// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>Reads the number arrays shared by the function types.</summary>
internal static class FunctionReader
{
    /// <summary>The numbers in a min/max pair.</summary>
    internal const int PairSize = 2;

    /// <summary>The /FunctionType of a sampled function.</summary>
    internal const int SampledType = 0;

    /// <summary>The /FunctionType of an exponential interpolation function.</summary>
    internal const int ExponentialType = 2;

    /// <summary>The /FunctionType of a stitching function.</summary>
    internal const int StitchingType = 3;

    /// <summary>The /FunctionType of a PostScript calculator function.</summary>
    internal const int PostScriptType = 4;

    /// <summary>The longest number array read.</summary>
    private const int MaxNumbers = 4096;

    /// <summary>Reads every item of an array as a number.</summary>
    /// <param name="array">The array.</param>
    /// <returns>The numbers, or <see langword="null"/> when the array is missing, too long or holds a non-number.</returns>
    internal static float[]? ReadNumbers(PdfArray? array)
    {
        if (array is null || array.Count > MaxNumbers)
        {
            return null;
        }

        var numbers = new float[array.Count];
        return array.ReadNumbers(numbers) == numbers.Length ? numbers : null;
    }

    /// <summary>Reads an array of min/max pairs, each with min not above max.</summary>
    /// <param name="array">The array.</param>
    /// <returns>The pairs, or <see langword="null"/> when missing or invalid.</returns>
    internal static float[]? ReadIntervals(PdfArray? array)
    {
        var numbers = ReadNumbers(array);
        if (numbers is null || numbers.Length == 0 || numbers.Length % PairSize != 0)
        {
            return null;
        }

        for (var i = 0; i < numbers.Length; i += PairSize)
        {
            if (!(numbers[i] <= numbers[i + 1]))
            {
                return null;
            }
        }

        return numbers;
    }

    /// <summary>Reads a number array of an exact length, or uses a fallback.</summary>
    /// <param name="array">The array.</param>
    /// <param name="length">The required length.</param>
    /// <param name="fallback">The value used when the array is missing.</param>
    /// <returns>The numbers, or <see langword="null"/> when present but invalid.</returns>
    internal static float[]? ReadExact(PdfArray? array, int length, float[] fallback)
    {
        if (array is null)
        {
            return fallback;
        }

        var numbers = ReadNumbers(array);
        return numbers?.Length >= length ? numbers : null;
    }
}
