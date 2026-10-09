// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Filters;

/// <summary>Decodes RunLengthDecode data.</summary>
internal static class RunLengthFilter
{
    /// <summary>The length byte that ends the data.</summary>
    private const byte EndOfData = 128;

    /// <summary>The value a repeat length byte is subtracted from.</summary>
    private const int RepeatBase = 257;

    /// <summary>Decodes literal and repeated runs.</summary>
    /// <param name="input">The encoded data.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    internal static void Decode(ReadOnlySpan<byte> input, ref PooledBuffer output)
    {
        var index = 0;
        while (index < input.Length)
        {
            var length = input[index];
            index++;
            if (length == EndOfData)
            {
                return;
            }

            if (length < EndOfData)
            {
                // A literal run of length + 1 bytes.
                var count = Math.Min(length + 1, input.Length - index);
                output.Write(input.Slice(index, count));
                index += count;
                continue;
            }

            if (index >= input.Length)
            {
                return;
            }

            var repeat = RepeatBase - length;
            output.GetSpan(repeat)[..repeat].Fill(input[index]);
            output.Advance(repeat);
            index++;
        }
    }
}
