// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Extensions;

/// <summary>The dictionary an unknown entry was found in.</summary>
public enum PdfEntryOwner
{
    /// <summary>The document catalog.</summary>
    Catalog = 0,

    /// <summary>A page dictionary.</summary>
    Page = 1,
}
