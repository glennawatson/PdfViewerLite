// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Fonts.Data;

/// <summary>
/// One sorted table of every glyph name the built-in data uses: the Adobe Glyph List, the encodings, the standard 14
/// fonts, the CFF standard strings and the Macintosh glyph order. Each name has a small id, and every lookup is a binary
/// search over static data, so nothing allocates.
/// </summary>
internal static partial class GlyphNames
{
    /// <summary>The id that means "no name".</summary>
    internal const int NoName = 0xFFFF;

    /// <summary>Gets the bytes of a name.</summary>
    /// <param name="id">The name id.</param>
    /// <returns>The ASCII bytes, or empty for an unknown id.</returns>
    internal static ReadOnlySpan<byte> Get(int id)
    {
        if ((uint)id >= Count)
        {
            return [];
        }

        var offsets = NameOffsets;
        int start = offsets[id];
        return NameData[start..offsets[id + 1]];
    }

    /// <summary>Finds the id of a name.</summary>
    /// <param name="name">The name's bytes.</param>
    /// <returns>The id, or <see cref="NoName"/> when the table does not hold it.</returns>
    internal static int Find(ReadOnlySpan<byte> name)
    {
        var low = 0;
        var high = Count - 1;
        while (low <= high)
        {
            var middle = (int)((uint)(low + high) >> 1);
            var order = Get(middle).SequenceCompareTo(name);
            if (order == 0)
            {
                return middle;
            }

            if (order < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return NoName;
    }

    /// <summary>Determines whether a name id refers to a name.</summary>
    /// <param name="id">The id.</param>
    /// <returns><see langword="true"/> when the id is in the table.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsName(int id) => (uint)id < Count;
}
