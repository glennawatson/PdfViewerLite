// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>The RC4 stream cipher, which older PDF encryption uses and .NET does not provide.</summary>
internal static class Rc4
{
    /// <summary>The size of the cipher state.</summary>
    private const int StateSize = 256;

    /// <summary>Encrypts or decrypts in place; RC4 is its own inverse.</summary>
    /// <param name="key">The key.</param>
    /// <param name="data">The data.</param>
    internal static void Apply(ReadOnlySpan<byte> key, Span<byte> data)
    {
        Span<byte> state = stackalloc byte[StateSize];
        for (var i = 0; i < StateSize; i++)
        {
            state[i] = (byte)i;
        }

        var j = 0;
        for (var i = 0; i < StateSize; i++)
        {
            j = (j + state[i] + key[i % key.Length]) & byte.MaxValue;
            var swap = state[i];
            state[i] = state[j];
            state[j] = swap;
        }

        var x = 0;
        var y = 0;
        for (var k = 0; k < data.Length; k++)
        {
            x = (x + 1) & byte.MaxValue;
            y = (y + state[x]) & byte.MaxValue;
            var swap = state[x];
            state[x] = state[y];
            state[y] = swap;
            data[k] ^= state[(state[x] + state[y]) & byte.MaxValue];
        }
    }
}
