// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>What the JP2 boxes around a codestream say, or the defaults for a raw codestream.</summary>
/// <param name="Codestream">Where the codestream is.</param>
/// <param name="ColorSpace">The declared colour space.</param>
/// <param name="Palette">The palette, or <see langword="null"/>.</param>
/// <param name="Mappings">The component mapping, or <see langword="null"/>.</param>
/// <param name="Definitions">The channel definitions, or <see langword="null"/>.</param>
[DebuggerDisplay("JpxFileInfo: {ColorSpace}")]
internal sealed record JpxFileInfo(JpxDataRange Codestream, JpxColorSpace ColorSpace, JpxPalette? Palette, JpxChannelMapping[]? Mappings, JpxChannelDefinition[]? Definitions);
