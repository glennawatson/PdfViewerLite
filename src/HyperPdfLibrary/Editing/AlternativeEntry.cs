// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Editing;

/// <summary>The text of one <c>rdf:li</c> entry in an XMP language alternative.</summary>
/// <param name="ContentStart">The index of the first byte of the entry's content.</param>
/// <param name="ContentEnd">The index after the entry's content.</param>
/// <param name="IsDefault">Whether the entry's language is <c>x-default</c>.</param>
[DebuggerDisplay("AlternativeEntry: {ContentStart}..{ContentEnd}")]
internal readonly record struct AlternativeEntry(int ContentStart, int ContentEnd, bool IsDefault);
