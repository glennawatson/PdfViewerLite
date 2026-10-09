// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.CMaps;

/// <summary>A codespace range: codes of one byte length whose every byte lies between the matching bytes of two bounds.</summary>
/// <param name="Length">The code length in bytes, one to four.</param>
/// <param name="Low">The lower bound, big-endian.</param>
/// <param name="High">The upper bound, big-endian.</param>
[DebuggerDisplay("CodespaceRange: {Length} bytes {Low:X}-{High:X}")]
internal readonly record struct CodespaceRange(int Length, uint Low, uint High)
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The mask of a byte.</summary>
    private const uint ByteMask = 0xFF;

    /// <summary>Determines whether bytes start with a code in this range.</summary>
    /// <param name="bytes">The bytes, at least <see cref="Length"/> long.</param>
    /// <returns><see langword="true"/> when every byte is within its bounds.</returns>
    internal bool Matches(ReadOnlySpan<byte> bytes)
    {
        for (var i = 0; i < Length; i++)
        {
            var shift = (Length - 1 - i) * ByteBits;
            var b = bytes[i];
            if (b < ((Low >> shift) & ByteMask) || b > ((High >> shift) & ByteMask))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether a byte can start a code in this range.</summary>
    /// <param name="first">The first byte.</param>
    /// <returns><see langword="true"/> when the byte is within the first byte's bounds.</returns>
    internal bool MatchesFirst(byte first)
    {
        var shift = (Length - 1) * ByteBits;
        return first >= ((Low >> shift) & ByteMask) && first <= ((High >> shift) & ByteMask);
    }
}
