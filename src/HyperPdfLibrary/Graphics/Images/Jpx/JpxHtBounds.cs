// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The magnitude exponent bounds U of the two quads of an HT quad pair.</summary>
/// <param name="First">The first quad's bound.</param>
/// <param name="Second">The second quad's bound.</param>
internal readonly record struct JpxHtBounds(int First, int Second)
{
    /// <summary>The residual mode in which both quads have a u-offset.</summary>
    internal const int Both = 3;
}
