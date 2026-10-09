// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Editing;

/// <summary>
/// The kinds of change an edit makes, in the categories DocMDP and FieldMDP permissions use: form filling and signing
/// are allowed from level 2, annotations from level 3, and everything else by no level.
/// </summary>
[Flags]
public enum PdfChangeKinds
{
    /// <summary>No change.</summary>
    None = 0,

    /// <summary>Annotations added, changed or removed (not form widgets).</summary>
    Annotations = 1 << 0,

    /// <summary>Form field values and their widget appearances changed.</summary>
    FormFill = 1 << 1,

    /// <summary>A signature field signed.</summary>
    Signing = 1 << 2,

    /// <summary>Pages inserted, deleted, moved or rotated, or the page tree changed.</summary>
    PageChanges = 1 << 3,

    /// <summary>The document information dictionary or the XMP metadata changed.</summary>
    Metadata = 1 << 4,

    /// <summary>Any other change, such as the outline, page labels or form structure.</summary>
    Other = 1 << 5,
}
