// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Rendering;

namespace PdfViewerLite.App.Rendering;

/// <summary>Creates <see cref="AvaloniaRenderSurface"/> instances on the render thread.</summary>
internal sealed class AvaloniaSurfaceFactory : IRenderSurfaceFactory
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IRenderSurface Create(int width, int height) => new AvaloniaRenderSurface(width, height);
}
