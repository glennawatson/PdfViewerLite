// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Optimizing;

/// <summary>One table of an sfnt font being written.</summary>
/// <param name="Tag">The four-byte tag.</param>
/// <param name="Content">The table's bytes, which may be a slice of the source font so unchanged tables are not copied first.</param>
[DebuggerDisplay("SfntEntry: {Tag:X8}, {Content.Length} bytes")]
internal readonly record struct SfntEntry(uint Tag, ReadOnlyMemory<byte> Content);
