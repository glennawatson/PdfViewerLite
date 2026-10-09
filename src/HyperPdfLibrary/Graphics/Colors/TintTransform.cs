// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Graphics.Functions;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// Converts Separation and DeviceN tints to RGB through the tint transform and the alternate space. Without a usable
/// transform, tints darken towards black: gray = 1 - the largest tint.
/// </summary>
/// <param name="Alternate">The alternate colour space.</param>
/// <param name="Function">The tint transform, or <see langword="null"/> when it is missing or does not fit.</param>
internal sealed record TintTransform(PdfColorSpace Alternate, PdfFunction? Function)
{
    /// <summary>Creates a tint transform, dropping a function whose inputs or outputs do not fit.</summary>
    /// <param name="alternate">The alternate space.</param>
    /// <param name="function">The parsed function, or <see langword="null"/>.</param>
    /// <param name="tints">The number of tint components.</param>
    /// <returns>The transform.</returns>
    internal static TintTransform Create(PdfColorSpace alternate, PdfFunction? function, int tints)
    {
        var fits = function is not null && function.InputCount == tints && function.OutputCount == alternate.Components;
        return new(alternate, fits ? function : null);
    }

    /// <summary>Converts tints to RGB.</summary>
    /// <param name="tints">The tints.</param>
    /// <param name="rgb">Receives red, green and blue.</param>
    [SkipLocalsInit]
    internal void ToRgb(ReadOnlySpan<float> tints, Span<float> rgb)
    {
        if (Function is null)
        {
            var largest = 0F;
            foreach (var tint in tints)
            {
                largest = Math.Max(largest, Math.Clamp(tint, 0, 1));
            }

            rgb[..PdfColorSpace.RgbComponents].Fill(1 - largest);
            return;
        }

        Span<float> alternate = stackalloc float[PdfColorSpace.MaxComponents];
        alternate = alternate[..Function.OutputCount];
        Function.Evaluate(tints, alternate);
        Alternate.ToRgb(alternate, rgb);
    }
}
