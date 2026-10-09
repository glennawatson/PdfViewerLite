// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.CMaps;

/// <summary>A code range collected while a CMap is parsed.</summary>
/// <param name="Low">The first code.</param>
/// <param name="High">The last code.</param>
/// <param name="Value">The value of the first code.</param>
/// <param name="Order">The position the range was added in, which breaks ties between equal low codes.</param>
[DebuggerDisplay("{Low}..{High} = {Value} (#{Order})")]
internal readonly record struct CodeRangeEntry(uint Low, uint High, int Value, int Order);
