// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Filters;

/// <summary>Decodes ASCIIHexDecode data.</summary>
internal static class AsciiHexFilter
{
    /// <summary>The hexadecimal digits that make one byte.</summary>
    private const int DigitsPerByte = 2;

    /// <summary>Decodes hexadecimal digits up to the '&gt;' end marker, ignoring white space.</summary>
    /// <param name="input">The encoded data.</param>
    /// <param name="output">The buffer receiving the decoded bytes.</param>
    internal static void Decode(ReadOnlySpan<byte> input, ref PooledBuffer output)
    {
        var end = input.IndexOf((byte)'>');
        if (end >= 0)
        {
            input = input[..end];
        }

        var destination = output.GetSpan((input.Length + 1) / DigitsPerByte);

        // Unbroken runs of digits decode with the vectorised converter; white space falls back to the scalar loop.
        if ((input.Length & 1) == 0 && Convert.FromHexString(input, destination, out _, out var written) == OperationStatus.Done)
        {
            output.Advance(written);
            return;
        }

        output.Advance(PdfStringDecoder.DecodeHex(input, destination));
    }
}
