// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts;

/// <summary>One CID's entry from a /W or /W2 array, in glyph units.</summary>
/// <param name="First">The first CID of the run.</param>
/// <param name="Last">The last CID of the run.</param>
/// <param name="Advance">The width, or for /W2 the vertical advance w1y.</param>
/// <param name="OriginX">The vertical origin's x (/W2 only).</param>
/// <param name="OriginY">The vertical origin's y (/W2 only).</param>
[DebuggerDisplay("CidMetric: {First}-{Last} {Advance}")]
internal readonly record struct CidMetric(int First, int Last, float Advance, float OriginX, float OriginY);
