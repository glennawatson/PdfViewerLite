// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.Core.Rendering;

/// <summary>
/// Runs every page rasterisation on a single dedicated thread, highest priority first. Requests that became stale
/// (their client advanced its generation without re-requesting them) are dropped before any work is done. Results are
/// handed back through <see cref="TryTakeCompleted"/>, and <c>completionCallback</c> is raised once per batch so the UI
/// can drain them on its own thread.
/// </summary>
[DebuggerDisplay("Queue {QueueLength}")]
public sealed class RenderScheduler : IDisposable
{
    /// <summary>The bit position of the priority within the queue ordering key.</summary>
    private const int PriorityShift = 48;

    /// <summary>How long to wait for the render thread to finish on dispose.</summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Guards the queue and pending map.</summary>
    private readonly Lock _gate = new();

    /// <summary>The work queue, ordered by priority then arrival.</summary>
    private readonly PriorityQueue<RenderRequest, long> _queue = new();

    /// <summary>Keys that are queued or rendered but not yet taken, mapped to the latest requested generation.</summary>
    private readonly Dictionary<TileKey, int> _pending = [];

    /// <summary>Completed renders waiting for the UI.</summary>
    private readonly ConcurrentQueue<RenderedTile> _completed = new();

    /// <summary>Wakes the render thread.</summary>
    private readonly SemaphoreSlim _signal = new(0);

    /// <summary>Creates surfaces.</summary>
    private readonly IRenderSurfaceFactory _surfaceFactory;

    /// <summary>Notifies the UI that completed tiles are waiting.</summary>
    private readonly Action _completionCallback;

    /// <summary>The render thread.</summary>
    private readonly Thread _thread;

    /// <summary>Cancels the render thread.</summary>
    private readonly CancellationTokenSource _shutdown = new();

    /// <summary>Monotonic counter preserving FIFO order within a priority.</summary>
    private long _sequence;

    /// <summary>1 while a completion notification is outstanding.</summary>
    private int _notificationPending;

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="RenderScheduler"/> class.</summary>
    /// <param name="surfaceFactory">Creates output surfaces on the render thread.</param>
    /// <param name="completionCallback">Raised on the render thread when completed tiles become available.</param>
    public RenderScheduler(IRenderSurfaceFactory surfaceFactory, Action completionCallback)
    {
        ArgumentNullException.ThrowIfNull(surfaceFactory);
        ArgumentNullException.ThrowIfNull(completionCallback);
        _surfaceFactory = surfaceFactory;
        _completionCallback = completionCallback;
        _thread = new(Run) { IsBackground = true, Name = "PdfViewerLite render" };
        _thread.Start();
    }

    /// <summary>Gets the number of queued requests, including stale ones not yet discarded.</summary>
    public int QueueLength
    {
        get
        {
            lock (_gate)
            {
                return _queue.Count;
            }
        }
    }

    /// <summary>Queues a render unless the same tile is already queued, in which case its generation is refreshed.</summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> when a new job was queued.</returns>
    public bool Request(in RenderRequest request)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        lock (_gate)
        {
            if (_pending.TryGetValue(request.Key, out var generation))
            {
                if (generation != request.Generation)
                {
                    _pending[request.Key] = request.Generation;
                }

                return false;
            }

            _pending.Add(request.Key, request.Generation);
            var sequence = _sequence;
            _sequence = sequence + 1;
            _queue.Enqueue(request, ((long)request.Priority << PriorityShift) | sequence);
        }

        _ = _signal.Release();
        return true;
    }

    /// <summary>Determines whether a tile is queued or awaiting collection.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when pending.</returns>
    public bool IsPending(in TileKey key)
    {
        lock (_gate)
        {
            return _pending.ContainsKey(key);
        }
    }

    /// <summary>Takes one completed tile. Call repeatedly from the UI thread after the completion callback fires.</summary>
    /// <param name="tile">The completed tile; the caller owns its surface.</param>
    /// <returns><see langword="true"/> when a tile was taken.</returns>
    public bool TryTakeCompleted(out RenderedTile tile)
    {
        // Clear the flag first so a render finishing during the drain raises a fresh notification.
        _ = Interlocked.Exchange(ref _notificationPending, 0);
        if (!_completed.TryDequeue(out tile))
        {
            return false;
        }

        lock (_gate)
        {
            _ = _pending.Remove(tile.Key);
        }

        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _shutdown.Cancel();
        _ = _thread.Join(ShutdownTimeout);
        while (_completed.TryDequeue(out var tile))
        {
            tile.Surface.Dispose();
        }

        _signal.Dispose();
        _shutdown.Dispose();
    }

    /// <summary>Renders a request into a surface.</summary>
    /// <param name="target">The locked surface.</param>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> when rendered.</returns>
    private static bool RenderInto(RenderTarget target, in RenderRequest request)
    {
        if (!request.Document.Render(request.Info, target))
        {
            return false;
        }

        if ((request.Info.Flags & RenderFlags.Invert) != 0)
        {
            PixelOperations.InvertColors(target);
        }

        return true;
    }

    /// <summary>The render thread loop.</summary>
    private void Run()
    {
        var token = _shutdown.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                _signal.Wait(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (!TryDequeue(out var request))
            {
                continue;
            }

            if (!Execute(request))
            {
                Forget(request.Key);
            }
        }
    }

    /// <summary>Dequeues the next live request, discarding stale ones.</summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> when a request should be rendered.</returns>
    private bool TryDequeue(out RenderRequest request)
    {
        lock (_gate)
        {
            if (!_queue.TryDequeue(out request, out _))
            {
                return false;
            }

            var latest = _pending.TryGetValue(request.Key, out var generation) ? generation : request.Generation;
            if (latest != request.Client.Generation || request.Document.IsDisposed)
            {
                _ = _pending.Remove(request.Key);
                return false;
            }

            return true;
        }
    }

    /// <summary>Renders one request and publishes the result.</summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> when a tile was published.</returns>
    private bool Execute(in RenderRequest request)
    {
        IRenderSurface? surface = null;
        try
        {
            surface = _surfaceFactory.Create(request.Width, request.Height);
            if (!surface.Write(request, RenderInto))
            {
                surface.Dispose();
                return false;
            }
        }
        catch (ObjectDisposedException)
        {
            surface?.Dispose();
            return false;
        }
        catch (InvalidOperationException ex)
        {
            Debug.WriteLine($"Render failed for {request.Key}: {ex.Message}");
            surface?.Dispose();
            return false;
        }

        _completed.Enqueue(new(request.Key, surface));
        if (Interlocked.Exchange(ref _notificationPending, 1) == 0)
        {
            _completionCallback();
        }

        return true;
    }

    /// <summary>Removes a key from the pending map after a failed or skipped render.</summary>
    /// <param name="key">The key.</param>
    private void Forget(in TileKey key)
    {
        lock (_gate)
        {
            _ = _pending.Remove(key);
        }
    }
}
