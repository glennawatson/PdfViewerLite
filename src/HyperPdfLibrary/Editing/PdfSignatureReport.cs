// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Editing;

/// <summary>The kinds of change pending edits make, and their effect on each signature field.</summary>
/// <param name="Kinds">The kinds of change.</param>
/// <param name="ChangedFields">The fully qualified names of the form fields whose values changed.</param>
/// <param name="Signatures">The effect on each signature field, in form order.</param>
[DebuggerDisplay("PdfSignatureReport: {Kinds}, {Signatures.Count} signatures")]
public sealed record PdfSignatureReport(PdfChangeKinds Kinds, IReadOnlyList<string> ChangedFields, IReadOnlyList<PdfSignatureEffect> Signatures);
