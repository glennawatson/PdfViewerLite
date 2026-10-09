// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Writing;

/// <summary>Which annotations a page copy keeps or draws into its content.</summary>
public enum PdfAnnotationFilter
{
    /// <summary>Every annotation.</summary>
    All = 0,

    /// <summary>Only form field widgets, so filled fields still print when notes and markup are left out.</summary>
    WidgetsOnly = 1,

    /// <summary>No annotations.</summary>
    None = 2,
}
