// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Interchange;

/// <summary>What an import changed in a document.</summary>
/// <param name="FieldsApplied">The fields whose value was set.</param>
/// <param name="FieldsSkipped">The fields left alone: unknown names, read-only fields and values that do not fit.</param>
/// <param name="AnnotationsAdded">The annotations added.</param>
/// <param name="AnnotationsReplaced">The annotations that replaced one of the same name on the same page.</param>
/// <param name="AnnotationsSkipped">The annotations left out: unsupported subtypes and pages the document does not have.</param>
[DebuggerDisplay("PdfInterchangeImportResult: {FieldsApplied} fields, {AnnotationsAdded} annotations")]
public readonly record struct PdfInterchangeImportResult(
    int FieldsApplied,
    int FieldsSkipped,
    int AnnotationsAdded,
    int AnnotationsReplaced,
    int AnnotationsSkipped);
