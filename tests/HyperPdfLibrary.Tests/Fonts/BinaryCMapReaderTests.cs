// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Generation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Checks compact resource decoding without network or generated files.</summary>
public sealed class BinaryCMapReaderTests
{
    /// <summary>Decodes two differential CID entries.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsDifferentialCidEntries()
    {
        var map = BinaryCMapReader.Parse([0, 0x41, 0x02, 0x82, 0xA0, 0x86, 0x4B, 0, 0]);
        await Assert.That(map.Ranges).IsEquivalentTo([new CidRange(0x82A0, 0x82A0, 0x034B), new CidRange(0x82A1, 0x82A1, 0x034C)]);
    }

    /// <summary>Decodes the UTF-16 value associated with a CID.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsUnicodeEntry()
    {
        var map = BinaryCMapReader.Parse([0, 0x81, 1, 0, 1, 0x30, 0x42]);
        await Assert.That(map.Unicode[1]).IsEqualTo(0x3042);
    }

    /// <summary>A UTF-16 surrogate pair remains one supplementary Unicode scalar.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsSupplementaryUnicodeEntry()
    {
        const int supplementary = 0x20000;
        var map = BinaryCMapReader.Parse([0, 0x83, 1, 0, 1, 0xD8, 0x40, 0xDC, 0]);
        await Assert.That(map.Unicode[1]).IsEqualTo(supplementary);
    }

    /// <summary>A Unicode range increments the encoded UTF-16 destination.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsSupplementaryUnicodeRange()
    {
        const int first = 0x20000;
        var map = BinaryCMapReader.Parse([0, 0xA3, 1, 0, 1, 1, 0xD8, 0x40, 0xDC, 0]);
        await Assert.That(map.Unicode[1]).IsEqualTo(first);
        await Assert.That(map.Unicode[2]).IsEqualTo(first + 1);
    }

    /// <summary>Truncated resources fail before any data can be published.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsTruncatedResource() =>
        await Assert.That(static () => BinaryCMapReader.Parse([0, 0x41, 1, 0x82])).Throws<InvalidDataException>();

    /// <summary>Unknown block kinds are rejected.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsUnknownBlock() =>
        await Assert.That(static () => BinaryCMapReader.Parse([0, 0xC0, 1])).Throws<InvalidDataException>();

    /// <summary>Inverse Unicode mappings fill gaps without replacing explicit values.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreservesExplicitUnicodeMappings()
    {
        int[] values = ['\0', 'X', '\0'];
        BinaryUnicodeFallback.Fill(values, [new(0x0041, 0x0042, 1)]);
        await Assert.That(values).IsEquivalentTo([0, 0x58, 0x42]);
    }

    /// <summary>Cancellation stops parsing and its scope does not affect the next parse.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledParseRestoresScope()
    {
        byte[] data = [0, 0x81, 1, 0, 1, 0x30, 0x42];
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.That(() => ParseWithToken(data, source.Token)).Throws<OperationCanceledException>();
        var map = ParseWithToken(data, CancellationToken.None);
        await Assert.That(map.Unicode[1]).IsEqualTo(0x3042);
    }

    /// <summary>Cancellation stops fallback before changing mappings.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledFallbackPreservesMappings()
    {
        int[] values = [0, 0];
        List<CidRange> ranges = [new(0x41, 0x41, 1)];
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.That(() => FillWithToken(values, ranges, source.Token)).Throws<OperationCanceledException>();
        await Assert.That(values[1]).IsEqualTo(0);
        FillWithToken(values, ranges, CancellationToken.None);
        await Assert.That(values[1]).IsEqualTo(0x41);
    }

    /// <summary>Cancellation stops both table packers and the next pack succeeds.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledPackingRestoresScope()
    {
        int[] values = [0, 0x41];
        var map = ParseWithToken([0, 0x41, 1, 0x41, 1, 0], CancellationToken.None);
        var maps = new Dictionary<string, BinaryCMapData>(StringComparer.Ordinal) { ["H"] = map };
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.That(() => PackUnicodeWithToken(values, source.Token)).Throws<OperationCanceledException>();
        await Assert.That(() => PackCMapWithToken(maps, source.Token)).Throws<OperationCanceledException>();
        await Assert.That(PackUnicodeWithToken(values, CancellationToken.None).Length).IsGreaterThan(0);
        await Assert.That(PackCMapWithToken(maps, CancellationToken.None).Length).IsGreaterThan(0);
    }

    /// <summary>Parses under a token that ends before any test awaits.</summary>
    /// <param name="data">The binary mapping.</param>
    /// <param name="cancellationToken">The scoped token.</param>
    /// <returns>The parsed mapping.</returns>
    private static BinaryCMapData ParseWithToken(byte[] data, CancellationToken cancellationToken)
    {
        using var scope = PdfCancellation.Enter(cancellationToken);
        return BinaryCMapReader.Parse(data);
    }

    /// <summary>Fills Unicode mappings under a scoped token.</summary>
    /// <param name="values">The CID mappings.</param>
    /// <param name="ranges">The fallback ranges.</param>
    /// <param name="cancellationToken">The scoped token.</param>
    private static void FillWithToken(int[] values, List<CidRange> ranges, CancellationToken cancellationToken)
    {
        using var scope = PdfCancellation.Enter(cancellationToken);
        BinaryUnicodeFallback.Fill(values, ranges);
    }

    /// <summary>Packs Unicode mappings under a scoped token.</summary>
    /// <param name="values">The CID mappings.</param>
    /// <param name="cancellationToken">The scoped token.</param>
    /// <returns>The packed table.</returns>
    private static byte[] PackUnicodeWithToken(int[] values, CancellationToken cancellationToken)
    {
        using var scope = PdfCancellation.Enter(cancellationToken);
        return PackedTables.PackUnicode(values);
    }

    /// <summary>Packs a CMap under a scoped token.</summary>
    /// <param name="maps">The parsed maps.</param>
    /// <param name="cancellationToken">The scoped token.</param>
    /// <returns>The packed table.</returns>
    private static byte[] PackCMapWithToken(Dictionary<string, BinaryCMapData> maps, CancellationToken cancellationToken)
    {
        using var scope = PdfCancellation.Enter(cancellationToken);
        return PackedTables.PackCMap(maps, "H", Collections.All[0]);
    }
}
