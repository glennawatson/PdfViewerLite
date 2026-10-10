// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Owns a native triangle mesh.</summary>
internal sealed class SkiaRenderVertices : IPdfRenderVertices
{
    /// <summary>The owned native vertices.</summary>
    private SKVertices? _native;

    /// <summary>Initializes a new instance of the <see cref="SkiaRenderVertices"/> class.</summary>
    /// <param name="native">The owned native resource.</param>
    internal SkiaRenderVertices(SKVertices native) => _native = native;

    /// <summary>Gets the borrowed native vertices while they are owned.</summary>
    /// <exception cref="ObjectDisposedException">The vertices have been disposed.</exception>
    internal SKVertices Native => Volatile.Read(ref _native) ?? throw new ObjectDisposedException(nameof(SkiaRenderVertices));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Interlocked.Exchange(ref _native, null)?.Dispose();
}
