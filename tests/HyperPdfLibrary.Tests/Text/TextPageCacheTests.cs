// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks the bounded cache of text pages.</summary>
[NotInParallel]
public sealed class TextPageCacheTests
{
    /// <summary>The cache capacity used by the tests.</summary>
    private const int Capacity = 2;

    /// <summary>The third page index, which evicts the least recently used page.</summary>
    private const int ThirdPage = 2;

    /// <summary>The builds: pages 0, 1 and 2, then page 1 again after it was evicted.</summary>
    private const int ExpectedBuilds = 4;

    /// <summary>The cache keeps the most recently used pages and builds an evicted page again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EvictsLeastRecentlyUsedPage()
    {
        var document = TextTestDocument.Open(TextTestDocument.Create("BT /F1 10 Tf 10 100 Td (A) Tj ET").ToBytes());
        var cache = new PdfTextPageCache(Capacity);
        var builds = 0;
        Func<int, int, PdfTextPage> build = (_, _) =>
        {
            builds++;
            return HyperPdfLibrary.Document.PdfDocumentText.ExtractText(document, 0);
        };

        var first = cache.GetOrAdd(0, 0, build);
        _ = cache.GetOrAdd(1, 0, build);
        _ = cache.GetOrAdd(0, 0, build);
        _ = cache.GetOrAdd(ThirdPage, 0, build);
        var again = cache.GetOrAdd(0, 0, build);
        _ = cache.GetOrAdd(1, 0, build);

        await Assert.That(again).IsSameReferenceAs(first);
        await Assert.That(builds).IsEqualTo(ExpectedBuilds);
    }
}
