// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Document;

/// <summary>Compares byte arrays by content, and lets a dictionary be searched with a span without copying it.</summary>
internal sealed class ByteArrayComparer : IEqualityComparer<byte[]>, IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>
{
    /// <summary>Gets the shared comparer.</summary>
    internal static ByteArrayComparer Instance { get; } = new();

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(byte[]? x, byte[]? y) => x is not null && y is not null && x.AsSpan().SequenceEqual(y);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ReadOnlySpan<byte> alternate, byte[] other) => alternate.SequenceEqual(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetHashCode(byte[] obj) => GetHashCode(obj.AsSpan());

    /// <inheritdoc/>
    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        var hash = default(HashCode);
        hash.AddBytes(alternate);
        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] Create(ReadOnlySpan<byte> alternate) => alternate.ToArray();
}
