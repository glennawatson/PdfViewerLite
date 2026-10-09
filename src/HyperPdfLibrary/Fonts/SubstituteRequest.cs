// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Fonts;

/// <summary>What a non-embedded font asks for, used to choose a system font in its place.</summary>
/// <param name="BaseFont">The /BaseFont name without a subset tag.</param>
/// <param name="Standard">The standard 14 font the name means, or <see cref="StandardFont.None"/>.</param>
/// <param name="Flags">The font descriptor flags.</param>
/// <param name="Weight">The weight from the font descriptor, or zero when it gives none.</param>
/// <param name="Script">The CJK script of a CID font, or <see cref="CjkScript.None"/>.</param>
[DebuggerDisplay("SubstituteRequest: {BaseFont}")]
internal readonly record struct SubstituteRequest(string BaseFont, StandardFont Standard, FontFlags Flags, int Weight, CjkScript Script);
