// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Optimizing;

/// <summary>One table of an sfnt font: its tag and where its bytes are.</summary>
/// <param name="Tag">The four-byte tag.</param>
/// <param name="Offset">The offset of its bytes in the font.</param>
/// <param name="Length">The length of its bytes.</param>
[DebuggerDisplay("SfntTable: {Tag:X8} at {Offset}, {Length} bytes")]
internal readonly record struct SfntTable(uint Tag, int Offset, int Length);
