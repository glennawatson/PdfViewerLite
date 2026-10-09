// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Interchange;

/// <summary>Which parts of a document an export writes.</summary>
[Flags]
public enum PdfInterchangeContent
{
    /// <summary>Writes nothing from the document.</summary>
    None = 0,

    /// <summary>The values of the form fields.</summary>
    Fields = 1 << 0,

    /// <summary>The annotations.</summary>
    Annotations = 1 << 1,

    /// <summary>Fields and annotations.</summary>
    All = Fields | Annotations,
}
