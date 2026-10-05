// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Rendering;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.Rendering;

/// <summary>
/// Owns the process wide tile cache and render scheduler. Completed tiles are moved into the cache on the UI thread in
/// batches, then <see cref="TilesArrived"/> lets visible controls repaint.
/// </summary>
[DebuggerDisplay("{Cache}")]
public sealed class RenderHub : IDisposable
{
    /// <summary>Emits on the UI thread after tiles were added to the cache.</summary>
    private readonly Signal<RxVoid> _tilesArrived = new();

    /// <summary>The subscription draining completed tiles.</summary>
    private readonly IDisposable _drainSubscription;

    /// <summary>Initializes a new instance of the <see cref="RenderHub"/> class.</summary>
    /// <param name="cacheBytes">The tile cache budget in bytes.</param>
    public RenderHub(long cacheBytes)
    {
        Cache = new(cacheBytes);
        Scheduler = new(new AvaloniaSurfaceFactory());
        TilesArrived = new(_tilesArrived);
        _drainSubscription = Scheduler.Completed
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeSafe(_ => Drain(), static ex => Trace.TraceError(ex.ToString()));
    }

    /// <summary>Gets notifications, on the UI thread, that new tiles were added to the cache.</summary>
    public AsObservableSignal<RxVoid> TilesArrived { get; }

    /// <summary>Gets the tile cache. Use only on the UI thread.</summary>
    public TileCache Cache { get; }

    /// <summary>Gets the render scheduler.</summary>
    public RenderScheduler Scheduler { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _drainSubscription.Dispose();
        Scheduler.Dispose();
        Cache.Dispose();
        _tilesArrived.OnCompleted();
        _tilesArrived.Dispose();
    }

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
            _tilesArrived.OnNext(RxVoid.Default);
        }
    }
}
