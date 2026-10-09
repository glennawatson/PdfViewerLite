// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Interchange;

/// <summary>Decodes the bytes an XFDF <c>data</c> element carries as text.</summary>
internal static class InterchangeBytes
{
    /// <summary>The hexadecimal digits in a byte.</summary>
    private const int DigitsPerByte = 2;

    /// <summary>The characters in a base64 group.</summary>
    private const int Base64Group = 4;

    /// <summary>The bytes in a base64 group.</summary>
    private const int Base64GroupBytes = 3;

    /// <summary>Decodes hexadecimal digits, ignoring white space.</summary>
    /// <param name="text">The digits.</param>
    /// <returns>The bytes, or <see langword="null"/> when the text is not hexadecimal.</returns>
    internal static byte[]? DecodeHex(string text)
    {
        var digits = text.AsSpan();
        var rented = ArrayPool<char>.Shared.Rent(Math.Max(digits.Length, 1));
        try
        {
            var count = 0;
            foreach (var c in digits)
            {
                if (char.IsWhiteSpace(c))
                {
                    continue;
                }

                rented[count] = c;
                count++;
            }

            return count % DigitsPerByte == 0 ? Convert.FromHexString(rented.AsSpan(0, count)) : null;
        }
        catch (FormatException)
        {
            return null;
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }

    /// <summary>Decodes base64 text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes, or <see langword="null"/> when the text is not base64.</returns>
    internal static byte[]? DecodeBase64(string text)
    {
        var buffer = new byte[(text.Length / Base64Group * Base64GroupBytes) + Base64GroupBytes];
        return Convert.TryFromBase64Chars(text, buffer, out var written) ? buffer.AsSpan(0, written).ToArray() : null;
    }

    /// <summary>Undoes Flate compression.</summary>
    /// <param name="compressed">The compressed bytes.</param>
    /// <returns>The bytes decoded before any damage.</returns>
    internal static byte[] Inflate(byte[] compressed)
    {
        var output = default(PooledBuffer);
        try
        {
            FlateFilter.Decode(compressed, ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }
}
