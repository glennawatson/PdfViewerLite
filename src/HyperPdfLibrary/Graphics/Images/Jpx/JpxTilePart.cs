// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>Where one tile-part's header and packet data sit in the codestream.</summary>
/// <param name="Tile">The tile index.</param>
/// <param name="Header">The tile-part header markers, between SOT and SOD.</param>
/// <param name="Data">The packet data after SOD.</param>
internal readonly record struct JpxTilePart(int Tile, JpxDataRange Header, JpxDataRange Data);
