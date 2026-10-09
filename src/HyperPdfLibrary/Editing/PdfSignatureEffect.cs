// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Editing;

/// <summary>How saving the pending edits would affect one signature field. A report, not a block.</summary>
/// <param name="FieldName">The signature field's fully qualified name.</param>
/// <param name="IsSigned">Whether the field holds a signature.</param>
/// <param name="IsCertification">Whether the signature certifies the document with DocMDP permissions.</param>
/// <param name="Permission">The DocMDP level that applies (1 no changes, 2 form filling and signing, 3 also annotations), or 0 for none.</param>
/// <param name="KeepsSignedBytes">
/// Whether the save leaves the signed bytes and the signature dictionary as they are: true for an incremental save that
/// does not touch the signature, false for a full rewrite.
/// </param>
/// <param name="DisallowedKinds">The kinds of change made that the permission level does not allow.</param>
/// <param name="LockedFieldsChanged">The fields locked by FieldMDP or /Lock whose values changed.</param>
[DebuggerDisplay("PdfSignatureEffect: {FieldName} valid {RemainsValid}")]
public sealed record PdfSignatureEffect(
    string FieldName,
    bool IsSigned,
    bool IsCertification,
    int Permission,
    bool KeepsSignedBytes,
    PdfChangeKinds DisallowedKinds,
    IReadOnlyList<string> LockedFieldsChanged)
{
    /// <summary>Gets a value indicating whether the signature would still validate with its permissions met.</summary>
    public bool RemainsValid => IsSigned && KeepsSignedBytes && DisallowedKinds == PdfChangeKinds.None && LockedFieldsChanged.Count == 0;
}
