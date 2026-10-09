// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>One lifting step of an inverse wavelet: updates samples from the sum of two neighbours.</summary>
/// <typeparam name="T">The sample type.</typeparam>
internal interface IJpxLiftStep<T>
    where T : unmanaged
{
    /// <summary>Updates each target sample from the samples at the same index in two neighbour runs.</summary>
    /// <param name="target">The samples updated.</param>
    /// <param name="first">The first neighbour of each sample.</param>
    /// <param name="second">The second neighbour of each sample.</param>
    /// <param name="factor">The step's lifting coefficient; the reversible steps ignore it.</param>
    static abstract void Apply(Span<T> target, ReadOnlySpan<T> first, ReadOnlySpan<T> second, float factor);
}
