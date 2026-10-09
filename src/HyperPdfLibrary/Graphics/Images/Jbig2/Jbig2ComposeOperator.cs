// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>How a region's pixels combine with the pixels already on the page or in a region (T.88 section 6.3.2).</summary>
internal enum Jbig2ComposeOperator
{
    /// <summary>Black wins: the source is ORed in.</summary>
    Or = 0,

    /// <summary>The source is ANDed in.</summary>
    And = 1,

    /// <summary>The source is XORed in.</summary>
    Xor = 2,

    /// <summary>The source is XNORed in.</summary>
    Xnor = 3,

    /// <summary>The source replaces the target.</summary>
    Replace = 4,
}
