// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>The corner of a symbol instance that its coordinates locate (T.88 REFCORNER).</summary>
internal enum Jbig2Corner
{
    /// <summary>The bottom-left corner.</summary>
    BottomLeft = 0,

    /// <summary>The top-left corner.</summary>
    TopLeft = 1,

    /// <summary>The bottom-right corner.</summary>
    BottomRight = 2,

    /// <summary>The top-right corner.</summary>
    TopRight = 3,
}
