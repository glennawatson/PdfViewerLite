// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>One kid of a structure element, in logical order.</summary>
/// <param name="Kind">What the kid is.</param>
/// <param name="Element">The child element, for <see cref="PdfStructureKidKind.Element"/>.</param>
/// <param name="PageIndex">The page the content or object is on, from the kid's <c>/Pg</c> or the element's; -1 when unknown.</param>
/// <param name="Mcid">The marked content id, for <see cref="PdfStructureKidKind.MarkedContent"/>; otherwise -1.</param>
/// <param name="Object">The referenced object, for <see cref="PdfStructureKidKind.Object"/>.</param>
/// <param name="Stream">The content stream holding the marked content, from an <c>/MCR</c>'s <c>/Stm</c>; not valid for page content.</param>
[DebuggerDisplay("PdfStructureKid: {Kind} page {PageIndex} mcid {Mcid}")]
public readonly record struct PdfStructureKid(
    PdfStructureKidKind Kind,
    PdfStructureElement? Element,
    int PageIndex,
    int Mcid,
    PdfObjectId Object,
    PdfObjectId Stream);
