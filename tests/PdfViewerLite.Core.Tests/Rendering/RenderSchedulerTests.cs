// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Rendering;
using PdfViewerLite.Core.Tests.Fakes;
using ReactiveUI.Primitives;

namespace PdfViewerLite.Core.Tests.Rendering;

/// <summary>Tests for <see cref="RenderScheduler"/>.</summary>
public sealed class RenderSchedulerTests
{
    /// <summary>The edge of test tiles.</summary>
    private const int Edge = 8;

    /// <summary>The document identifier of the document used to occupy the render thread.</summary>
    private const int BlockerDocument = 99;

    /// <summary>The page still wanted after the view moves.</summary>
    private const int WantedPage = 2;

    /// <summary>The test document file name.</summary>
    private const string DocumentName = "a.pdf";

    /// <summary>How long to wait for asynchronous work.</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>Verifies a request is rendered and handed back once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RendersAndCompletes()
    {
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var document = new FakeDocument(DocumentName, FakeEngine.A4);
        var client = new RenderClient();
        var key = Key(0);

        await Assert.That(scheduler.Request(Request(key, document, client, RenderPriority.Visible))).IsTrue();
        await Assert.That(await completed.WaitAsync(Timeout)).IsTrue();
        await Assert.That(scheduler.TryTakeCompleted(out var tile)).IsTrue();

        await Assert.That(tile.Key).IsEqualTo(key);
        await Assert.That(document.RenderCount).IsEqualTo(1);
        await Assert.That(scheduler.IsPending(key)).IsFalse();
        tile.Surface.Dispose();
    }

    /// <summary>Verifies duplicate requests are coalesced.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CoalescesDuplicates()
    {
        using var gate = new ManualResetEventSlim(false);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        var document = new FakeDocument(DocumentName, FakeEngine.A4) { Gate = gate };
        var client = new RenderClient();

        var first = scheduler.Request(Request(Key(0), document, client, RenderPriority.Visible));
        var second = scheduler.Request(Request(Key(0), document, client, RenderPriority.Visible));
        gate.Set();

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
    }

    /// <summary>Verifies requests made stale by advancing the client generation are never rendered.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DropsStaleRequests()
    {
        using var gate = new ManualResetEventSlim(false);
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var blocker = new FakeDocument("blocker.pdf", FakeEngine.A4) { Gate = gate };
        var document = new FakeDocument(DocumentName, FakeEngine.A4, FakeEngine.A4, FakeEngine.A4);
        var client = new RenderClient();
        var blockerClient = new RenderClient();
        const int stalePages = 3;

        // Occupy the render thread so the following requests queue up.
        _ = scheduler.Request(Request(new(BlockerDocument, 0, 1, PageRotation.None, 0, 0, 0), blocker, blockerClient, RenderPriority.Visible));
        _ = blocker.Started.Wait(Timeout);
        for (var page = 0; page < stalePages; page++)
        {
            _ = scheduler.Request(Request(Key(page), document, client, RenderPriority.Visible));
        }

        // The view moved: only page 2 is still wanted.
        var generation = client.Advance();
        _ = scheduler.Request(Request(Key(WantedPage), document, client, RenderPriority.Visible) with { Generation = generation });
        gate.Set();

        var taken = new List<TileKey>();
        while (taken.Count < WantedPage && await completed.WaitAsync(Timeout))
        {
            while (scheduler.TryTakeCompleted(out var tile))
            {
                taken.Add(tile.Key);
                tile.Surface.Dispose();
            }
        }

        await Assert.That(document.RenderCount).IsEqualTo(1);
        await Assert.That(taken).Contains(Key(WantedPage));
        await Assert.That(scheduler.IsPending(Key(0))).IsFalse();
    }

    /// <summary>Verifies higher priority work runs first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RunsHigherPriorityFirst()
    {
        using var gate = new ManualResetEventSlim(false);
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var blocker = new FakeDocument("blocker.pdf", FakeEngine.A4) { Gate = gate };
        var document = new FakeDocument(DocumentName, FakeEngine.A4, FakeEngine.A4);
        var client = new RenderClient();
        var blockerKey = new TileKey(BlockerDocument, 0, 1, PageRotation.None, 0, 0, 0);
        const int expectedTiles = 3;

        _ = scheduler.Request(Request(blockerKey, blocker, client, RenderPriority.Visible));
        _ = blocker.Started.Wait(Timeout);
        _ = scheduler.Request(Request(Key(0), document, client, RenderPriority.Thumbnail));
        _ = scheduler.Request(Request(Key(1), document, client, RenderPriority.VisiblePreview));
        gate.Set();

        var order = new List<TileKey>();
        while (order.Count < expectedTiles && await completed.WaitAsync(Timeout))
        {
            while (scheduler.TryTakeCompleted(out var tile))
            {
                order.Add(tile.Key);
                tile.Surface.Dispose();
            }
        }

        await Assert.That(order).IsEquivalentTo([blockerKey, Key(1), Key(0)]);
        await Assert.That(order[0]).IsEqualTo(blockerKey);
        await Assert.That(order[1]).IsEqualTo(Key(1));
    }

    /// <summary>Verifies the request's page tone recolours rendered pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppliesPageTone()
    {
        const uint paper = 0x2A2826U;
        const uint ink = 0xD2CDC5U;
        const byte inkBlue = 0xC5;
        using var completed = new SemaphoreSlim(0);
        using var scheduler = new RenderScheduler(new FakeSurfaceFactory());
        using var completions = scheduler.Completed.SubscribeSafe(_ => completed.Release(), static _ => { });
        var document = new FakeDocument(DocumentName, FakeEngine.A4);
        var request = Request(Key(0), document, new(), RenderPriority.Visible) with { Tone = new(paper, ink) };

        _ = scheduler.Request(request);
        _ = await completed.WaitAsync(Timeout);
        _ = scheduler.TryTakeCompleted(out var tile);

        // The fake renders page 0 as all-zero bytes (black), which the tone maps to the ink colour.
        await Assert.That(((FakeSurface)tile.Surface).Pixels[0]).IsEqualTo(inkBlue);
        tile.Surface.Dispose();
    }

    /// <summary>Creates a key for a page.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The key.</returns>
    private static TileKey Key(int page) => new(1, page, 1, PageRotation.None, 0, 0, 0);

    /// <summary>Creates a request.</summary>
    /// <param name="key">The key.</param>
    /// <param name="document">The document.</param>
    /// <param name="client">The client.</param>
    /// <param name="priority">The priority.</param>
    /// <returns>The request.</returns>
    private static RenderRequest Request(in TileKey key, IDocument document, RenderClient client, RenderPriority priority) =>
        new(key, document, new(key.PageIndex, 1, PageRotation.None, 0, 0, RenderFlags.None), Edge, Edge, priority, client, client.Generation, PageTone.None);
}
