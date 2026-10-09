// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.PageObjects;

/// <summary>How a path is painted.</summary>
public enum PdfPathPaintMode
{
    /// <summary>The path is not painted.</summary>
    None = 0,

    /// <summary>Stroked only.</summary>
    Stroke = 1,

    /// <summary>Filled only.</summary>
    Fill = 2,

    /// <summary>Filled, then stroked.</summary>
    FillStroke = 3,
}
