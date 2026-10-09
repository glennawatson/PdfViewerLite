// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>The values a Type 1 font's cleartext part gives.</summary>
/// <param name="Matrix">The font matrix.</param>
/// <param name="Box">The font bounding box.</param>
/// <param name="Standard">Whether the built-in encoding is StandardEncoding.</param>
/// <param name="Encoding">The explicit encoding entries.</param>
[DebuggerDisplay("Type1Header: {Encoding.Length} encoding entries")]
internal readonly record struct Type1Header(FontMatrix Matrix, PdfRectangle Box, bool Standard, Type1EncodingEntry[] Encoding);
