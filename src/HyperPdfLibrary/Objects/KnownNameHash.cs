// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Objects;

/// <summary>Stable hashes shared by the known-name lookup and its table generator.</summary>
internal static class KnownNameHash
{
    /// <summary>The multiplier for spellings shorter than one word.</summary>
    private const uint Prime = 16_777_619;

    /// <summary>The bytes in each sampled word.</summary>
    private const int WordBytes = 4;

    /// <summary>The rotation separating the first and last sampled words.</summary>
    private const int SampleRotation = 13;

    /// <summary>The displacement multiplier.</summary>
    private const uint DisplacementPrime = 0x9E3779B9;

    /// <summary>The first avalanche multiplier.</summary>
    private const uint MixFirst = 0x85EBCA6B;

    /// <summary>The second avalanche multiplier.</summary>
    private const uint MixSecond = 0xC2B2AE35;

    /// <summary>The first and final avalanche shifts.</summary>
    private const int OuterShift = 16;

    /// <summary>The middle avalanche shift.</summary>
    private const int InnerShift = 13;

    /// <summary>Hashes the length and edge bytes; the generator verifies distinct hashes for every known name.</summary>
    /// <param name="spelling">The decoded UTF-8 spelling.</param>
    /// <returns>The stable hash.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Hash(ReadOnlySpan<byte> spelling)
    {
        var hash = (uint)spelling.Length;
        if (spelling.Length >= WordBytes)
        {
            return hash ^ BinaryPrimitives.ReadUInt32LittleEndian(spelling)
                ^ BitOperations.RotateLeft(BinaryPrimitives.ReadUInt32LittleEndian(spelling[^WordBytes..]), SampleRotation);
        }

        foreach (var value in spelling)
        {
            hash = unchecked((hash * Prime) ^ value);
        }

        return hash;
    }

    /// <summary>Maps a spelling hash into a displaced bucket.</summary>
    /// <param name="hash">The stable spelling hash.</param>
    /// <param name="displacement">The generated bucket displacement.</param>
    /// <returns>The mixed hash.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Displace(uint hash, int displacement)
    {
        var mixed = unchecked(hash + ((uint)displacement * DisplacementPrime));
        mixed = unchecked((mixed ^ (mixed >> OuterShift)) * MixFirst);
        mixed = unchecked((mixed ^ (mixed >> InnerShift)) * MixSecond);
        return mixed ^ (mixed >> OuterShift);
    }
}
