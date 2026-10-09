// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.CMaps;

/// <summary>
/// Maps character codes to values through sorted, inclusive code ranges, so a large CJK range costs one entry. A lookup
/// is a binary search; where ranges overlap, the one with the highest start wins, and among equal starts the later one.
/// </summary>
[DebuggerDisplay("CodeRangeMap: {Count} ranges")]
internal sealed class CodeRangeMap
{
    /// <summary>The first code of each range, sorted.</summary>
    private readonly uint[] _lows;

    /// <summary>The last code of each range.</summary>
    private readonly uint[] _highs;

    /// <summary>The largest last code of this and every earlier range, which bounds the backward search over overlaps.</summary>
    private readonly uint[] _maxHighs;

    /// <summary>The value of each range's first code.</summary>
    private readonly int[] _values;

    /// <summary>Initializes a new instance of the <see cref="CodeRangeMap"/> class.</summary>
    /// <param name="lows">The first codes, sorted.</param>
    /// <param name="highs">The last codes.</param>
    /// <param name="values">The values of the first codes.</param>
    internal CodeRangeMap(uint[] lows, uint[] highs, int[] values)
    {
        _lows = lows;
        _highs = highs;
        _values = values;
        _maxHighs = new uint[highs.Length];
        uint max = 0;
        for (var i = 0; i < highs.Length; i++)
        {
            max = Math.Max(max, highs[i]);
            _maxHighs[i] = max;
        }
    }

    /// <summary>Gets an empty map.</summary>
    internal static CodeRangeMap Empty { get; } = new([], [], []);

    /// <summary>Gets the number of ranges.</summary>
    internal int Count => _lows.Length;

    /// <summary>Finds the range holding a code.</summary>
    /// <param name="code">The code.</param>
    /// <param name="offset">The code's distance from the start of its range.</param>
    /// <param name="value">The value of the range's first code.</param>
    /// <returns><see langword="true"/> when a range holds the code.</returns>
    internal bool TryFind(uint code, out uint offset, out int value)
    {
        for (var i = UpperBound(code) - 1; i >= 0 && _maxHighs[i] >= code; i--)
        {
            if (_highs[i] < code)
            {
                continue;
            }

            offset = code - _lows[i];
            value = _values[i];
            return true;
        }

        offset = 0;
        value = 0;
        return false;
    }

    /// <summary>Finds the number of ranges that start at or before a code.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The count.</returns>
    private int UpperBound(uint code)
    {
        var low = 0;
        var high = _lows.Length;
        while (low < high)
        {
            var middle = (int)((uint)(low + high) >> 1);
            if (_lows[middle] <= code)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
