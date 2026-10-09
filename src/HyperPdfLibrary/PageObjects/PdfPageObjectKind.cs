// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.PageObjects;

/// <summary>The kinds of object a page's content stream paints.</summary>
public enum PdfPageObjectKind
{
    /// <summary>No kind.</summary>
    None = 0,

    /// <summary>One text-showing operator: <c>Tj</c>, <c>TJ</c>, <c>'</c> or <c>"</c>.</summary>
    Text = 1,

    /// <summary>A path that is stroked or filled.</summary>
    Path = 2,

    /// <summary>An image XObject or an inline image.</summary>
    Image = 3,

    /// <summary>A shading painted with <c>sh</c>.</summary>
    Shading = 4,

    /// <summary>A form XObject.</summary>
    Form = 5,
}
