// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Fonts;

/// <summary>The facts the simple font encoding rules depend on.</summary>
/// <param name="Initial">The base encoding chosen before /Encoding is read; <see cref="FontEncoding.None"/> for built-in.</param>
/// <param name="IsEmbedded">Whether the font program is embedded.</param>
/// <param name="IsTrueType">Whether the glyphs come from a TrueType face.</param>
/// <param name="IsSymbolic">Whether the descriptor flags say symbolic.</param>
/// <param name="IsSymbolName">Whether the base font is named Symbol.</param>
[DebuggerDisplay("EncodingSettings: {Initial}")]
internal readonly record struct EncodingSettings(FontEncoding Initial, bool IsEmbedded, bool IsTrueType, bool IsSymbolic, bool IsSymbolName);
