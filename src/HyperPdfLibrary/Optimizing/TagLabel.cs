// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>How one unit is marked: a structure tag with a marked content id, or an artifact when the id is negative.</summary>
/// <param name="Tag">The structure type written as the marked content tag.</param>
/// <param name="Mcid">The marked content id, or -1 for an artifact.</param>
[DebuggerDisplay("TagLabel: {Tag} {Mcid}")]
internal readonly record struct TagLabel(PdfName Tag, int Mcid)
{
    /// <summary>Gets a value indicating whether the unit is an artifact.</summary>
    internal bool IsArtifact => Mcid < 0;
}
