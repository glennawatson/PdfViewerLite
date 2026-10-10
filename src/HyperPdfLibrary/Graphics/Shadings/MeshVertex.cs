// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Graphics.Shadings;

/// <summary>A mesh vertex: a position and either a resolved colour or, for shadings with a function, the function input.</summary>
/// <param name="Point">The position in shading space.</param>
/// <param name="Color">The colour, when the shading has no function.</param>
/// <param name="T">The function input, when the shading has a function.</param>
[DebuggerDisplay("MeshVertex: {Point}")]
internal readonly record struct MeshVertex(PdfPoint Point, PdfColor Color, float T)
{
    /// <summary>Blends two vertices.</summary>
    /// <param name="a">The first vertex.</param>
    /// <param name="b">The second vertex.</param>
    /// <param name="amount">The share of <paramref name="b"/>, from 0 to 1.</param>
    /// <returns>The blended colour and input, at the position of <paramref name="a"/>.</returns>
    internal static MeshVertex Mix(in MeshVertex a, in MeshVertex b, float amount)
    {
        var red = Lerp(a.Color.Red, b.Color.Red, amount);
        var green = Lerp(a.Color.Green, b.Color.Green, amount);
        var blue = Lerp(a.Color.Blue, b.Color.Blue, amount);
        return new(a.Point, new((byte)red, (byte)green, (byte)blue), a.T + ((b.T - a.T) * amount));
    }

    /// <summary>Interpolates a channel.</summary>
    /// <param name="a">The first value.</param>
    /// <param name="b">The second value.</param>
    /// <param name="amount">The share of <paramref name="b"/>.</param>
    /// <returns>The rounded result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(byte a, byte b, float amount) => MathF.Round(a + ((b - a) * amount));
}
