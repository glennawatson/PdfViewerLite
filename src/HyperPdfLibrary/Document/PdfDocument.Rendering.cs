// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Document;

/// <content>Rendering caches.</content>
public sealed partial class PdfDocument
{
    /// <summary>Guards the list of renderers.</summary>
    private readonly Lock _renderersGate = new();

    /// <summary>The renderers made for the document, held weakly so an unused renderer can be collected.</summary>
    private readonly List<WeakReference<PdfPageRenderer>> _renderers = [];

    /// <summary>The render cache, made on first use.</summary>
    private PdfRenderCache? _renderCache;

    /// <summary>Gets what the document keeps between renders.</summary>
    internal PdfRenderCache RenderCache
    {
        get
        {
            if (Volatile.Read(ref _renderCache) is { } existing)
            {
                return existing;
            }

            _ = Interlocked.CompareExchange(ref _renderCache, new(this), null);
            return Volatile.Read(ref _renderCache)!;
        }
    }

    /// <summary>Records a renderer, so disposing the document releases the renderer's page pictures.</summary>
    /// <param name="renderer">The renderer.</param>
    internal void RegisterRenderer(PdfPageRenderer renderer)
    {
        lock (_renderersGate)
        {
            _ = _renderers.RemoveAll(static reference => !reference.TryGetTarget(out _));
            _renderers.Add(new(renderer));
        }
    }

    /// <summary>Releases the page pictures of every renderer and empties the render caches and the text pages.</summary>
    private void ReleaseRendering()
    {
        WeakReference<PdfPageRenderer>[] renderers;
        lock (_renderersGate)
        {
            renderers = [.. _renderers];
            _renderers.Clear();
        }

        foreach (var reference in renderers)
        {
            if (reference.TryGetTarget(out var renderer))
            {
                renderer.Dispose();
            }
        }

        Volatile.Read(ref _renderCache)?.Close();
        Volatile.Read(ref _intentRenderCache)?.Close();
        Volatile.Read(ref _textPages)?.Clear();
    }
}
