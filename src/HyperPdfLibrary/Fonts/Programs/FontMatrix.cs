// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>An affine transform from font units to text space: x' = A·x + C·y + E and y' = B·x + D·y + F.</summary>
/// <param name="A">The x scale.</param>
/// <param name="B">The y shear.</param>
/// <param name="C">The x shear.</param>
/// <param name="D">The y scale.</param>
/// <param name="E">The x offset.</param>
/// <param name="F">The y offset.</param>
[DebuggerDisplay("[{A} {B} {C} {D} {E} {F}]")]
public readonly record struct FontMatrix(float A, float B, float C, float D, float E, float F)
{
    /// <summary>The usual Type 1 and CFF scale of one thousandth.</summary>
    private const float Thousandth = 0.001F;

    /// <summary>Gets the identity transform.</summary>
    public static FontMatrix Identity => new(1, 0, 0, 1, 0, 0);

    /// <summary>Gets the default Type 1 and CFF matrix, which maps 1000 units to one text space unit.</summary>
    public static FontMatrix Default => new(Thousandth, 0, 0, Thousandth, 0, 0);

    /// <summary>Creates a scale.</summary>
    /// <param name="scale">The scale on both axes.</param>
    /// <returns>The matrix.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FontMatrix FromScale(float scale) => new(scale, 0, 0, scale, 0, 0);

    /// <summary>Applies this transform after another.</summary>
    /// <param name="inner">The transform applied first.</param>
    /// <returns>The combined transform.</returns>
    public FontMatrix Multiply(FontMatrix inner) => new(
        (A * inner.A) + (C * inner.B),
        (B * inner.A) + (D * inner.B),
        (A * inner.C) + (C * inner.D),
        (B * inner.C) + (D * inner.D),
        (A * inner.E) + (C * inner.F) + E,
        (B * inner.E) + (D * inner.F) + F);

    /// <summary>Transforms the x coordinate of a point.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The transformed x.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float TransformX(float x, float y) => (A * x) + (C * y) + E;

    /// <summary>Transforms the y coordinate of a point.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    /// <returns>The transformed y.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float TransformY(float x, float y) => (B * x) + (D * y) + F;
}
