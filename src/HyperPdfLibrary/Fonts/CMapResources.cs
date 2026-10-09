// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// The packed tables in Fonts/Data/CMaps are built by scripts/CMapTableGenerator.cs from Adobe's cmap-resources
// and mapping-resources-pdf (Copyright 1990-2023 and 1990-2019 Adobe, BSD 3-Clause). Fonts/Data/CMaps/LICENSE.txt holds the notice and is embedded beside the
// tables.

using System.Buffers;
using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// Reads the predefined CMaps and CID-to-Unicode tables embedded in the assembly. Each resource is a zlib stream; the
/// callers cache what they build, so each resource is read at most once.
/// </summary>
internal static class CMapResources
{
    /// <summary>The prefix of the resource names.</summary>
    private const string Prefix = "HyperPdfLibrary.CMaps.";

    /// <summary>The longest name looked up; longer names are never predefined CMaps.</summary>
    private const int MaxNameLength = 32;

    /// <summary>Reads and decompresses a resource.</summary>
    /// <param name="name">The table name, such as <c>90ms-RKSJ-H</c> or <c>Adobe-Japan1-UCS2</c>.</param>
    /// <param name="output">Receives the decompressed bytes after any it holds.</param>
    /// <returns><see langword="true"/> when the resource exists.</returns>
    internal static bool TryRead(string name, ref PooledBuffer output)
    {
        if (name.Length is 0 or > MaxNameLength)
        {
            return false;
        }

        using var stream = typeof(CMapResources).Assembly.GetManifestResourceStream(Prefix + name);
        if (stream is null)
        {
            return false;
        }

        var length = (int)stream.Length;
        var packed = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            stream.ReadExactly(packed, 0, length);
            _ = ZLibCodec.Decompress(packed.AsSpan(0, length), false, ref output);
            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(packed);
        }
    }
}
