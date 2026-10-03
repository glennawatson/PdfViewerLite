// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Platform;
using ReactiveUI.Primitives;

namespace PdfViewerLite.Core.Tests.Platform;

/// <summary>Tests for <see cref="PipeSingleInstance"/>, the single window used on Windows and macOS; named pipes also work on Linux.</summary>
public sealed class PipeSingleInstanceTests
{
    /// <summary>How long to wait for a forwarded request.</summary>
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    /// <summary>A second launch hands its documents to the first and cannot claim the window itself.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ForwardsToTheRunningInstance()
    {
        var name = $"PdfViewerLiteTest-{Guid.NewGuid():N}";
        using var first = PipeSingleInstance.TryStart(name);
        var received = new TaskCompletionSource<OpenRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = first!.OpenRequests.SubscribeSafe(request => received.TrySetResult(request), static _ => { });

        var second = PipeSingleInstance.TryStart(name);
        var forwarded = await PipeSingleInstance.TryForwardAsync(name, new(["/home/me/a.pdf", "https://example.com/b.pdf"], "token"));
        var request = await received.Task.WaitAsync(Wait);

        await Assert.That(second).IsNull();
        await Assert.That(forwarded).IsTrue();
        await Assert.That(request.Uris).IsEquivalentTo(["/home/me/a.pdf", "https://example.com/b.pdf"]);
        await Assert.That(request.ActivationToken).IsEqualTo("token");
    }

    /// <summary>With nothing running, forwarding fails quickly so the launch opens its own window.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsWhenNothingIsRunning() =>
        await Assert.That(await PipeSingleInstance.TryForwardAsync($"PdfViewerLiteTest-{Guid.NewGuid():N}", new([], null))).IsFalse();

    /// <summary>Messages round trip, dropping anything with a line break and an empty token.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EncodesRequests()
    {
        var decoded = PipeSingleInstance.Decode(PipeSingleInstance.Encode(new(["a.pdf", "bad\nname.pdf", "c d.pdf"], null)));

        await Assert.That(decoded.Uris).IsEquivalentTo(["a.pdf", "c d.pdf"]);
        await Assert.That(decoded.ActivationToken).IsNull();
        await Assert.That(PipeSingleInstance.NameFor("App")).StartsWith("App-");
    }
}
