// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Where one marked content id's glyphs sit in a page's glyph order.</summary>
/// <param name="Start">The first position.</param>
/// <param name="Count">The number of glyphs.</param>
[DebuggerDisplay("McidRange: {Start} + {Count}")]
internal readonly record struct McidRange(int Start, int Count);
