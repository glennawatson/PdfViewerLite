// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;

namespace HyperPdfLibrary.Optimizing;

/// <summary>A SHA-256 digest used as a dictionary key: equal digests mean equal content.</summary>
/// <param name="First">Bytes 0 to 7.</param>
/// <param name="Second">Bytes 8 to 15.</param>
/// <param name="Third">Bytes 16 to 23.</param>
/// <param name="Fourth">Bytes 24 to 31.</param>
[DebuggerDisplay("ContentHash: {First:X16}")]
internal readonly record struct ContentHash(ulong First, ulong Second, ulong Third, ulong Fourth)
{
    /// <summary>The bytes of a digest.</summary>
    internal const int Length = 32;

    /// <summary>The bytes of one part.</summary>
    private const int PartLength = 8;

    /// <summary>The offset of the third part.</summary>
    private const int ThirdOffset = 16;

    /// <summary>The offset of the fourth part.</summary>
    private const int FourthOffset = 24;

    /// <summary>Hashes bytes.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The digest.</returns>
    internal static ContentHash Of(ReadOnlySpan<byte> data)
    {
        Span<byte> digest = stackalloc byte[Length];
        _ = SHA256.HashData(data, digest);
        return FromBytes(digest);
    }

    /// <summary>Reads a digest.</summary>
    /// <param name="digest">The 32 bytes.</param>
    /// <returns>The digest.</returns>
    internal static ContentHash FromBytes(ReadOnlySpan<byte> digest) =>
        new(
            BinaryPrimitives.ReadUInt64LittleEndian(digest),
            BinaryPrimitives.ReadUInt64LittleEndian(digest[PartLength..]),
            BinaryPrimitives.ReadUInt64LittleEndian(digest[ThirdOffset..]),
            BinaryPrimitives.ReadUInt64LittleEndian(digest[FourthOffset..]));

    /// <summary>Writes the digest.</summary>
    /// <param name="destination">At least 32 bytes.</param>
    internal void CopyTo(Span<byte> destination)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(destination, First);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[PartLength..], Second);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[ThirdOffset..], Third);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[FourthOffset..], Fourth);
    }
}
