// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>One inferred structure element of a page: its type and the units it owns, in content order.</summary>
/// <param name="Tag">The structure type.</param>
/// <param name="Units">The indices of the units it owns.</param>
/// <param name="IsFigure">Whether it is a figure, which needs alternative text.</param>
[DebuggerDisplay("TagBlock: {Tag}, {Units.Count} units")]
internal sealed record TagBlock(PdfName Tag, List<int> Units, bool IsFigure)
{
    /// <summary>Gets the first unit's index, which places the block in content order.</summary>
    internal int FirstUnit => Units.Count > 0 ? Units[0] : int.MaxValue;
}
