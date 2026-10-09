// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.CMaps;

/// <summary>Finds a CMap by name, for usecmap and for predefined CMaps the library does not hold.</summary>
/// <param name="name">The CMap name's bytes, such as <c>UniJIS-UCS2-H</c>.</param>
/// <returns>The CMap, or <see langword="null"/> when unknown.</returns>
public delegate CMap? CMapResolver(ReadOnlySpan<byte> name);
