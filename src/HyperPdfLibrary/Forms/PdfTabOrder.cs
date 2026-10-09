// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Forms;

/// <summary>The order a page's annotations are visited with the Tab key (<c>/Tabs</c>, ISO 32000-2 12.5.1).</summary>
public enum PdfTabOrder
{
    /// <summary>The page names no order, so annotations are visited in <c>/Annots</c> order.</summary>
    Unspecified = 0,

    /// <summary>Row order: left to right along each row, rows from top to bottom (<c>/R</c>).</summary>
    Row = 1,

    /// <summary>Column order: top to bottom down each column, columns from left to right (<c>/C</c>).</summary>
    Column = 2,

    /// <summary>Structure order: the order of the annotations in the structure tree (<c>/S</c>).</summary>
    Structure = 3,
}
