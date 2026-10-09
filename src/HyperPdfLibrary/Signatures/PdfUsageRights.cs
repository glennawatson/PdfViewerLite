// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signatures;

/// <summary>The usage rights (UR3 transform parameters) a reader extension grants, read as data.</summary>
/// <param name="Document">The /Document rights, for example <c>FullSave</c>.</param>
/// <param name="Annotations">The /Annots rights, for example <c>Create</c>.</param>
/// <param name="Form">The /Form rights, for example <c>FillIn</c>.</param>
/// <param name="Signature">The /Signature rights, for example <c>Modify</c>.</param>
/// <param name="EmbeddedFiles">The /EF rights, for example <c>Import</c>.</param>
/// <param name="Message">The /Msg text shown when the rights are in effect, or <see langword="null"/>.</param>
/// <param name="RestrictsOtherRights">The /P flag: whether rights not listed are denied even to full viewers.</param>
[DebuggerDisplay("PdfUsageRights: {Document.Length} document rights")]
public sealed record PdfUsageRights(
    string[] Document,
    string[] Annotations,
    string[] Form,
    string[] Signature,
    string[] EmbeddedFiles,
    string? Message,
    bool RestrictsOtherRights);
