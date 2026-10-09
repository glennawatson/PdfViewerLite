// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>The coding process named by a JPEG frame header.</summary>
internal enum JpegProcess
{
    /// <summary>Sequential Huffman coding (SOF0 and SOF1).</summary>
    Sequential = 0,

    /// <summary>Progressive Huffman coding (SOF2).</summary>
    Progressive = 1,

    /// <summary>Lossless, hierarchical or arithmetic coding, which the managed decoder does not read.</summary>
    Unsupported = 2,
}
