// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Owns a native recorded picture.</summary>
internal sealed class SkiaRenderPicture : IPdfRenderPicture
{
    /// <summary>The owned native recording.</summary>
    private SKPicture? _native;

    /// <summary>Initializes a new instance of the <see cref="SkiaRenderPicture"/> class.</summary>
    /// <param name="native">The owned picture.</param>
    internal SkiaRenderPicture(SKPicture native)
    {
        _native = native;
        ApproximateBytesUsed = (long)native.ApproximateBytesUsed;
    }

    /// <inheritdoc/>
    public long ApproximateBytesUsed { get; }

    /// <summary>Gets the borrowed native recording while it is owned.</summary>
    /// <exception cref="ObjectDisposedException">The recording has been disposed.</exception>
    internal SKPicture Native => Volatile.Read(ref _native) ?? throw new ObjectDisposedException(nameof(SkiaRenderPicture));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Interlocked.Exchange(ref _native, null)?.Dispose();
}
