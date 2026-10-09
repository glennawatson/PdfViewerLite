// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>The outcome of decoding one Huffman-coded value.</summary>
internal enum Jbig2HuffmanResult
{
    /// <summary>A value was decoded.</summary>
    Value = 0,

    /// <summary>The out-of-band code was decoded.</summary>
    OutOfBand = 1,

    /// <summary>The data ended or held no valid code.</summary>
    Error = 2,
}
