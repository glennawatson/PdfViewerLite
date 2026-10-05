// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using Avalonia.Platform;
using PdfViewerLite.Skia.Rendering;

namespace PdfViewerLite.Skia.Gpu;

/// <summary>Adapts an <see cref = "ISkiaGpuRenderTarget"/> to Avalonia's <see cref = "IRenderTarget"/> pipeline.</summary>
/// <param name="skiaGpu">The shared graphics context.</param>
/// <param name="renderTarget">The owned render target.</param>
internal sealed class SkiaGpuRenderTarget(ISkiaGpu skiaGpu, ISkiaGpuRenderTarget renderTarget) : IRenderTarget
{
    /// <summary>The render target.</summary>
    private readonly ISkiaGpuRenderTarget _renderTarget = renderTarget;

    /// <inheritdoc/>
    public PlatformRenderTargetState PlatformRenderTargetState => _renderTarget.State;

    /// <inheritdoc/>
    public RenderTargetProperties Properties { get; }

    /// <summary>Gets the skia gpu.</summary>
    private ISkiaGpu SkiaGpu { get; } = skiaGpu;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _renderTarget.Dispose();

    /// <inheritdoc/>
    public IDrawingContextImpl CreateDrawingContext(IRenderTarget.RenderTargetSceneInfo sceneInfo, out RenderTargetDrawingContextProperties properties)
    {
        properties = default;
        var session = _renderTarget.BeginRenderingSession(sceneInfo);
        var createInfo = new DrawingContextImpl.CreateInfo
        {
            GrContext = session.GrContext,
            Surface = session.SkSurface,
            Dpi = SkiaPlatform.DefaultDpi * session.ScaleFactor,
            ScaleDrawingToDpi = false,
            Gpu = SkiaGpu,
            CurrentSession = session,
        };
        return new DrawingContextImpl(createInfo, session);
    }
}
