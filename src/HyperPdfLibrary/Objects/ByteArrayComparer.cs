// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Objects;

/// <summary>Compares byte arrays by content, and looks them up by span without allocating.</summary>
internal sealed class ByteArrayComparer : IEqualityComparer<byte[]>, IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>
{
    /// <summary>Gets the shared instance.</summary>
    internal static ByteArrayComparer Instance { get; } = new();

    /// <inheritdoc/>
    public bool Equals(byte[]? x, byte[]? y) => ReferenceEquals(x, y) || (x is not null && y is not null && x.AsSpan().SequenceEqual(y));

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ReadOnlySpan<byte> alternate, byte[] other) => alternate.SequenceEqual(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetHashCode(byte[] obj) => Hash(obj);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetHashCode(ReadOnlySpan<byte> alternate) => Hash(alternate);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] Create(ReadOnlySpan<byte> alternate) => alternate.ToArray();

    /// <summary>Hashes bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The hash code.</returns>
    private static int Hash(ReadOnlySpan<byte> bytes)
    {
        var hash = default(HashCode);
        hash.AddBytes(bytes);
        return hash.ToHashCode();
    }
}
