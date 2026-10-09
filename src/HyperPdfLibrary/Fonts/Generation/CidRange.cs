// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>A range of codes that map to consecutive CIDs.</summary>
/// <param name="Low">The first code.</param>
/// <param name="High">The last code.</param>
/// <param name="Cid">The CID of the first code.</param>
internal readonly record struct CidRange(uint Low, uint High, int Cid);
