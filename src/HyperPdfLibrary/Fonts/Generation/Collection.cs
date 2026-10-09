// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>One Adobe character collection and the tables built for it.</summary>
/// <param name="Script">The <c>CjkScript</c> value stored in the packed CMaps.</param>
/// <param name="UnicodeTable">The name of the packed CID-to-Unicode table.</param>
/// <param name="CMaps">The names of the CMaps packed for the collection.</param>
internal sealed record Collection(byte Script, string UnicodeTable, string[] CMaps);
