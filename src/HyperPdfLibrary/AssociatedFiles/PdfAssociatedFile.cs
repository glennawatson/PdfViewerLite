// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.AssociatedFiles;

/// <summary>A file associated with a part of the document through <c>/AF</c>.</summary>
/// <param name="Owner">The kind of object that lists the file.</param>
/// <param name="PageIndex">The zero based page the owner is on, or null for the catalog and objects not tied to a page.</param>
/// <param name="Relationship">The <c>/AFRelationship</c> name: Source, Data, Alternative, Supplement, EncryptedPayload, FormData, Schema or Unspecified.</param>
/// <param name="Name">The file name from the file specification.</param>
/// <param name="Description">The <c>/Desc</c> text, or null.</param>
/// <param name="MimeType">The embedded stream's <c>/Subtype</c> (a MIME type), or null.</param>
/// <param name="Data">The embedded file stream, or null when the specification has none.</param>
/// <param name="Specification">The file specification dictionary.</param>
[DebuggerDisplay("PdfAssociatedFile: {Owner} {Relationship} {Name}")]
public sealed record PdfAssociatedFile(
    PdfAssociatedOwner Owner,
    int? PageIndex,
    string Relationship,
    string Name,
    string? Description,
    string? MimeType,
    PdfStream? Data,
    PdfDictionary Specification);
