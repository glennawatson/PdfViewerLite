// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Writing;

/// <summary>One cross-reference entry to write.</summary>
/// <param name="Number">The object number.</param>
/// <param name="Type">The entry type.</param>
/// <param name="Location">The file offset, the containing object stream, or the next free number.</param>
/// <param name="Detail">The generation, or the index within the object stream.</param>
internal readonly record struct XrefRow(int Number, XrefEntryType Type, long Location, int Detail)
{
    /// <summary>The generation of the free entry at object zero.</summary>
    internal const int FreeHeadGeneration = 65_535;

    /// <summary>Gets the free entry at object zero, the head of the free list.</summary>
    internal static XrefRow FreeHead => new(0, XrefEntryType.Free, 0, FreeHeadGeneration);
}
