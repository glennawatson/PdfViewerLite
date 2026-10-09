// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Compat;

/// <summary>
/// Compresses data as zlib at the best compression level, for the optimiser. On .NET 11 the span-based
/// <c>ZLibEncoder</c> writes straight into the output; on .NET 10 <see cref="ZLibStream"/> does the same work.
/// </summary>
internal static class ZLibSmallest
{
#if NET11_0_OR_GREATER
    /// <summary>The highest compression level zlib offers.</summary>
    private const int BestQuality = 9;

    /// <summary>The window size, as a power of two: zlib's largest.</summary>
    private const int WindowLog2 = 15;
#endif

    /// <summary>Compresses data as zlib at the best level.</summary>
    /// <param name="input">The data.</param>
    /// <param name="output">Receives the compressed bytes.</param>
    internal static void Compress(ReadOnlySpan<byte> input, ref PooledBuffer output)
    {
#if NET11_0_OR_GREATER
        using var encoder = new ZLibEncoder(BestQuality, WindowLog2);
        var max = (int)Math.Min(int.MaxValue, ZLibEncoder.GetMaxCompressedLength(input.Length));
        _ = encoder.Compress(input, output.GetSpan(max), out _, out var written, isFinalBlock: true);
        output.Advance(written);
#else
        using var stream = new MemoryStream();
        using (var zlib = new ZLibStream(stream, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(input);
        }

        output.Write(stream.GetBuffer().AsSpan(0, (int)stream.Length));
#endif
    }
}
