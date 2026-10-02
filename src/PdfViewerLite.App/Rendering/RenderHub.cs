// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using PdfViewerLite.Core.Rendering;

namespace PdfViewerLite.App.Rendering;

/// <summary>
/// Owns the process wide tile cache and render scheduler. Completed tiles are moved into the cache on the UI thread in
/// batches, then <see cref="TilesArrived"/> lets visible controls repaint.
/// </summary>
[DebuggerDisplay("{Cache}")]
public sealed class RenderHub : IDisposable
{
    /// <summary>The cached drain callback, avoiding a delegate allocation per batch.</summary>
    private readonly Action _drain;

    /// <summary>Initializes a new instance of the <see cref="RenderHub"/> class.</summary>
    /// <param name="cacheBytes">The tile cache budget in bytes.</param>
    public RenderHub(long cacheBytes)
    {
        Cache = new(cacheBytes);
        _drain = Drain;
        Scheduler = new(new AvaloniaSurfaceFactory(), OnTilesCompleted);
    }

    /// <summary>Raised on the UI thread after new tiles were added to the cache.</summary>
    public event EventHandler? TilesArrived;

    /// <summary>Gets the tile cache. Use only on the UI thread.</summary>
    public TileCache Cache { get; }

    /// <summary>Gets the render scheduler.</summary>
    public RenderScheduler Scheduler { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Scheduler.Dispose();
        Cache.Dispose();
    }

    /// <summary>Schedules a drain on the UI thread; called on the render thread.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OnTilesCompleted() => Dispatcher.UIThread.Post(_drain, DispatcherPriority.Render);

    /// <summary>Moves completed tiles into the cache.</summary>
    private void Drain()
    {
        var any = false;
        while (Scheduler.TryTakeCompleted(out var tile))
        {
            Cache.Add(tile.Key, tile.Surface);
            any = true;
        }

        if (any)
        {
            TilesArrived?.Invoke(this, EventArgs.Empty);
        }
    }
}
