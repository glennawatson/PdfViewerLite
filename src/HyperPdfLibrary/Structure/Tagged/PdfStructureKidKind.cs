// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>What a kid of a structure element is.</summary>
public enum PdfStructureKidKind
{
    /// <summary>A child structure element.</summary>
    Element = 0,

    /// <summary>Marked content on a page, or in a stream given by an <c>/MCR</c> dictionary's <c>/Stm</c>.</summary>
    MarkedContent = 1,

    /// <summary>A whole object, such as an annotation, given by an <c>/OBJR</c> dictionary.</summary>
    Object = 2,
}
