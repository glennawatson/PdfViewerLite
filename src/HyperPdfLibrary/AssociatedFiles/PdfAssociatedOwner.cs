// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.AssociatedFiles;

/// <summary>The kind of object an associated file belongs to.</summary>
public enum PdfAssociatedOwner
{
    /// <summary>The document catalog.</summary>
    Document = 0,

    /// <summary>A page.</summary>
    Page = 1,

    /// <summary>An annotation.</summary>
    Annotation = 2,

    /// <summary>A structure element.</summary>
    StructureElement = 3,

    /// <summary>An image or form XObject.</summary>
    XObject = 4,

    /// <summary>Another object, such as a font or a content item.</summary>
    Other = 5,
}
