// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signatures;

/// <summary>The kinds of change an incremental update made after a signature.</summary>
[Flags]
public enum PdfModificationKinds
{
    /// <summary>No change.</summary>
    None = 0,

    /// <summary>A comment or markup annotation was added, changed or removed.</summary>
    Annotation = 1 << 0,

    /// <summary>A form field's value or widget changed.</summary>
    FormFill = 1 << 1,

    /// <summary>A signature field was signed or added.</summary>
    Signature = 1 << 2,

    /// <summary>Long-term validation data was added: the /DSS store or a document timestamp.</summary>
    SecurityStore = 1 << 3,

    /// <summary>Document information or XMP metadata changed.</summary>
    Metadata = 1 << 4,

    /// <summary>Pages were added, removed or had their content or properties changed.</summary>
    Pages = 1 << 5,

    /// <summary>Any other object changed.</summary>
    Other = 1 << 6,
}
