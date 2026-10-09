// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// The state one decode shares between segments: the pixels it may still decode or composite, and scratch bitmaps
/// reused for each symbol and each refined instance so those loops allocate nothing. Hostile data can ask for huge
/// symbols or endless instances; charging every step against a fixed budget bounds the time spent.
/// </summary>
[DebuggerDisplay("Jbig2Workspace: {Remaining} left")]
internal sealed class Jbig2Workspace : IDisposable
{
    /// <summary>The scratch bitmap for symbols being decoded, or <see langword="null"/> before first use.</summary>
    private Jbig2Bitmap? _symbol;

    /// <summary>The scratch bitmap for refined text region instances, or <see langword="null"/> before first use.</summary>
    private Jbig2Bitmap? _refinement;

    /// <summary>Initializes a new instance of the <see cref="Jbig2Workspace"/> class with <see cref="Jbig2Limits.MaxWork"/>.</summary>
    internal Jbig2Workspace() => Remaining = Jbig2Limits.MaxWork;

    /// <summary>Gets the work left.</summary>
    internal long Remaining { get; private set; }

    /// <summary>Returns the scratch bitmaps' buffers to the pool.</summary>
    public void Dispose()
    {
        _symbol?.Dispose();
        _refinement?.Dispose();
        _symbol = null;
        _refinement = null;
    }

    /// <summary>Spends work.</summary>
    /// <param name="amount">The pixels about to be decoded or composited; at least one is always charged.</param>
    /// <returns><see langword="false"/> when the budget is spent.</returns>
    internal bool TryCharge(long amount)
    {
        Remaining -= Math.Max(amount, 1);
        return Remaining >= 0;
    }

    /// <summary>Gets the white scratch bitmap for a symbol, sized for it. It is valid until the next call.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The bitmap, or <see langword="null"/> when the size is not valid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Jbig2Bitmap? Symbol(int width, int height) => Reuse(ref _symbol, width, height);

    /// <summary>Gets the white scratch bitmap for a refined instance, sized for it. It is valid until the next call.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The bitmap, or <see langword="null"/> when the size is not valid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Jbig2Bitmap? Refinement(int width, int height) => Reuse(ref _refinement, width, height);

    /// <summary>Resizes a scratch bitmap, creating it on first use.</summary>
    /// <param name="scratch">The scratch bitmap.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The bitmap, or <see langword="null"/> when the size is not valid.</returns>
    private static Jbig2Bitmap? Reuse(ref Jbig2Bitmap? scratch, int width, int height)
    {
        if (scratch is null)
        {
            scratch = Jbig2Bitmap.Create(width, height);
            return scratch;
        }

        return scratch.TryReshape(width, height) ? scratch : null;
    }
}
