// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Tests.Fakes;

namespace PdfViewerLite.Core.Tests.Documents;

/// <summary>Tests for <see cref="DocumentPool"/>.</summary>
public sealed class DocumentPoolTests
{
    /// <summary>Verifies documents are opened lazily and the least recently used are closed beyond capacity.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosesLeastRecentlyUsedBeyondCapacity()
    {
        const int capacity = 2;
        const int sourceCount = 3;
        var engine = new FakeEngine();
        using var pool = new DocumentPool(engine, capacity);
        var sources = new DocumentSource[sourceCount];
        for (var i = 0; i < sourceCount; i++)
        {
            sources[i] = pool.Create($"{i}.pdf", null);
        }

        await Assert.That(engine.Opened.Count).IsEqualTo(0);
        _ = sources[0].Acquire();
        await Task.Delay(1);
        _ = sources[1].Acquire();
        await Task.Delay(1);
        _ = sources[2].Acquire();

        await Assert.That(pool.OpenCount).IsEqualTo(capacity);
        await Assert.That(sources[0].IsOpen).IsFalse();
        await Assert.That(sources[0].PageCount).IsEqualTo(engine.Opened[0].PageCount);
        await Assert.That(engine.Opened[0].IsDisposed).IsTrue();
    }

    /// <summary>Verifies reload closes the document and changes the identifier.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReloadChangesIdentifier()
    {
        var engine = new FakeEngine();
        using var pool = new DocumentPool(engine);
        var source = pool.Create("a.pdf", null);
        _ = source.Acquire();
        var id = source.Id;

        source.Reload();

        await Assert.That(source.Id).IsNotEqualTo(id);
        await Assert.That(source.IsOpen).IsFalse();
        await Assert.That(source.PageCount).IsEqualTo(0);
        const int expectedOpens = 2;
        _ = source.Acquire();
        await Assert.That(engine.Opened.Count).IsEqualTo(expectedOpens);
    }
}
