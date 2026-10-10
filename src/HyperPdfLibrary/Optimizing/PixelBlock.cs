// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Optimizing;

/// <summary>The bounds of a block averaged during managed image reduction.</summary>
/// <param name="Left">The inclusive left column.</param>
/// <param name="Top">The inclusive top row.</param>
/// <param name="Right">The exclusive right column.</param>
/// <param name="Bottom">The exclusive bottom row.</param>
internal readonly record struct PixelBlock(int Left, int Top, int Right, int Bottom);
