// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Reading;

namespace PdfViewerLite.Pdfium;

/// <summary>What a structure element type means for reading: a block of some kind, a grouping to walk into, or unknown.</summary>
/// <param name="Kind">The block kind, for a block.</param>
/// <param name="Level">The heading level, 1 to 6, for a heading; zero otherwise.</param>
/// <param name="IsGrouping">Whether the element only groups others, such as a section, list or table row.</param>
/// <param name="IsUnknown">Whether the type is not a standard one, so it is a block only when it has no child elements.</param>
[DebuggerDisplay("{Kind} {Level} grouping={IsGrouping} unknown={IsUnknown}")]
internal readonly record struct StructureRole(ReadingBlockKind Kind, int Level, bool IsGrouping, bool IsUnknown);
