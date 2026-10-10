// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Owns a native shader.</summary>
internal sealed class SkiaRenderShader : IPdfRenderShader
{
    /// <summary>The owned native shader.</summary>
    private SKShader? _native;

    /// <summary>Initializes a new instance of the <see cref="SkiaRenderShader"/> class.</summary>
    /// <param name="native">The owned native resource.</param>
    internal SkiaRenderShader(SKShader native) => _native = native;

    /// <summary>Gets the borrowed native shader while it is owned.</summary>
    /// <exception cref="ObjectDisposedException">The shader has been disposed.</exception>
    internal SKShader Native => Volatile.Read(ref _native) ?? throw new ObjectDisposedException(nameof(SkiaRenderShader));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Interlocked.Exchange(ref _native, null)?.Dispose();
}
