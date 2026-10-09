// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>A Type 1 font's built-in encoding as read from the cleartext.</summary>
/// <param name="Standard">Whether the encoding is StandardEncoding.</param>
/// <param name="Entries">The explicit entries.</param>
[DebuggerDisplay("Standard {Standard}, {Entries.Length} entries")]
internal readonly record struct Type1EncodingSection(bool Standard, Type1EncodingEntry[] Entries);
