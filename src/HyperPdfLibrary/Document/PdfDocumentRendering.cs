// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Document;

/// <summary>Renders pages and manages renderer caches.</summary>
public static class PdfDocumentRendering
{
    /// <summary>Gets what the document keeps between renders.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The normal page render cache.</returns>
    internal static PdfRenderCache GetRenderCache(PdfDocument document)
    {
        if (Volatile.Read(ref document.State.RenderCache) is { } existing)
        {
            return existing;
        }

        _ = Interlocked.CompareExchange(ref document.State.RenderCache, new(document), null);
        return Volatile.Read(ref document.State.RenderCache)!;
    }

    /// <summary>Records a renderer, so disposing the document releases the renderer's page pictures.</summary>
    /// <param name="document">The document.</param>
    /// <param name="renderer">The renderer.</param>
    internal static void RegisterRenderer(PdfDocument document, PdfPageRenderer renderer)
    {
        lock (document.State.RenderersGate)
        {
            _ = document.State.Renderers.RemoveAll(static reference => !reference.TryGetTarget(out _));
            document.State.Renderers.Add(new(renderer));
        }
    }

    /// <summary>Drops font-dependent results made before newly requested resources were available.</summary>
    /// <param name="document">The document.</param>
    internal static void RefreshFontData(PdfDocument document)
    {
        var generation = FontDataResources.Generation;
        if (Volatile.Read(ref document.State.FontDataGeneration) == generation)
        {
            return;
        }

        lock (document.State.RenderersGate)
        {
            if (document.State.FontDataGeneration == generation)
            {
                return;
            }

            Volatile.Read(ref document.State.RenderCache)?.InvalidateFonts();
            Volatile.Read(ref document.State.IntentRenderCache)?.InvalidateFonts();
            PdfDocumentPageContent.InvalidatePageContent(document);
            Volatile.Write(ref document.State.FontDataGeneration, generation);
        }
    }

    /// <summary>Releases the page pictures of every renderer and empties the render caches and the text pages.</summary>
    /// <param name="document">The document.</param>
    internal static void ReleaseRendering(PdfDocument document)
    {
        WeakReference<PdfPageRenderer>[] renderers;
        lock (document.State.RenderersGate)
        {
            renderers = [.. document.State.Renderers];
            document.State.Renderers.Clear();
        }

        foreach (var reference in renderers)
        {
            if (reference.TryGetTarget(out var renderer))
            {
                renderer.Dispose();
            }
        }

        Volatile.Read(ref document.State.RenderCache)?.Close();
        Volatile.Read(ref document.State.IntentRenderCache)?.Close();
        Volatile.Read(ref document.State.TextPages)?.Clear();
    }
}
