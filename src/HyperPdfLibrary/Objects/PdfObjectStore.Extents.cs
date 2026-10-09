// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Objects;

/// <content>Where objects lie in the file, so loading ahead can read the bytes an object will need.</content>
public sealed partial class PdfObjectStore
{
    /// <summary>The offsets of the objects in the original file, sorted; made on first use.</summary>
    private long[]? _sortedOffsets;

    /// <summary>
    /// Finds the bytes an object's definition spans in the original file: from its offset to the next object's. A compressed
    /// object spans its object stream.
    /// </summary>
    /// <param name="number">The object number.</param>
    /// <param name="offset">Receives the first byte.</param>
    /// <param name="length">Receives the number of bytes.</param>
    /// <returns><see langword="false"/> when the object is not in the file.</returns>
    internal bool TryGetExtent(int number, out long offset, out long length)
    {
        offset = GetDefinitionOffset(number);
        length = 0;
        if (offset < 0)
        {
            return false;
        }

        var offsets = SortedOffsets();
        var at = Array.BinarySearch(offsets, offset);
        var end = at >= 0 && at + 1 < offsets.Length ? offsets[at + 1] : _source.Length;
        length = Math.Max(0, end - offset);
        return length > 0;
    }

    /// <summary>Gets the sorted offsets of every object in the original file.</summary>
    /// <returns>The offsets.</returns>
    private long[] SortedOffsets()
    {
        if (Volatile.Read(ref _sortedOffsets) is { } existing)
        {
            return existing;
        }

        var offsets = new List<long>(XrefSize);
        for (var number = 1; number < XrefSize; number++)
        {
            var offset = GetDefinitionOffset(number);
            if (offset >= 0)
            {
                offsets.Add(offset);
            }
        }

        offsets.Sort();
        var sorted = offsets.ToArray();
        _ = Interlocked.CompareExchange(ref _sortedOffsets, sorted, null);
        return Volatile.Read(ref _sortedOffsets)!;
    }
}
