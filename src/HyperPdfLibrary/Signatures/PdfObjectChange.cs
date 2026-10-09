// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signatures;

/// <summary>An object an incremental update added, changed or removed after a signature.</summary>
/// <param name="ObjectNumber">The object number.</param>
/// <param name="Kind">What the change is.</param>
/// <param name="FieldName">The fully qualified name of the form field it changes, or <see langword="null"/>.</param>
/// <param name="IsPermitted">Whether the signature's DocMDP and field locks allow the change.</param>
[DebuggerDisplay("PdfObjectChange: {ObjectNumber} {Kind}")]
public readonly record struct PdfObjectChange(int ObjectNumber, PdfModificationKinds Kind, string? FieldName, bool IsPermitted);
