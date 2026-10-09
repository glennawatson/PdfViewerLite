// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Tests for the decoded size cap.</summary>
public sealed class DecodeLimitTests
{
    /// <summary>How far past the cap the sample inflates.</summary>
    private const int Overshoot = 1 << 24;

    /// <summary>The size of the zero chunks fed to the compressor.</summary>
    private const int ChunkLength = 1 << 20;

    /// <summary>Gets a Flate filter followed by one that shrinks the zeros the sample decodes to into nothing.</summary>
    private static ReadOnlySpan<byte> Chain => "[/FlateDecode /ASCIIHexDecode]"u8;

    /// <summary>
    /// Data that inflates past the cap keeps what decoded before the cap and skips the rest of the filter chain, so the
    /// following filter does not run over it.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InflatingPastTheCapKeepsPartialOutputAndSkipsTheChain()
    {
        var compressed = CompressedZeros((long)PdfLimits.MaxDecodedLength + Overshoot);

        var length = DecodeChain(compressed);

        await Assert.That(length).IsGreaterThan(0);
        await Assert.That(length).IsLessThanOrEqualTo(PdfLimits.MaxDecodedLength);
    }

    /// <summary>Data that stays under the cap runs through the whole chain.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DataUnderTheCapRunsThroughTheChain()
    {
        var compressed = CompressedZeros(ChunkLength);

        await Assert.That(DecodeChain(compressed)).IsEqualTo(0);
    }

    /// <summary>Compresses a run of zero bytes without holding the whole run in memory.</summary>
    /// <param name="length">The number of zero bytes.</param>
    /// <returns>The zlib data.</returns>
    private static byte[] CompressedZeros(long length)
    {
        using var sink = new MemoryStream();
        using (var zlib = new ZLibStream(sink, CompressionLevel.Fastest, leaveOpen: true))
        {
            var chunk = new byte[ChunkLength];
            for (long written = 0; written < length; written += chunk.Length)
            {
                zlib.Write(chunk);
            }
        }

        return sink.ToArray();
    }

    /// <summary>Runs Flate then ASCIIHex over data and measures the output.</summary>
    /// <param name="compressed">The zlib data.</param>
    /// <returns>The number of bytes the chain produced.</returns>
    private static int DecodeChain(byte[] compressed)
    {
        var filters = new PdfParser(Chain.ToArray(), 0, null, new()).ParseValue();
        var output = default(PooledBuffer);
        try
        {
            var codec = PdfStreamDecoder.Apply(compressed, filters, default, ref output);
            return codec == PdfImageCodec.None ? output.Length : -1;
        }
        finally
        {
            output.Dispose();
        }
    }
}
