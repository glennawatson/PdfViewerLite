// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>A repair or limit the library met while reading a damaged or unusual file.</summary>
/// <param name="Code">What happened.</param>
/// <param name="Message">A short description.</param>
/// <param name="ObjectNumber">The object involved, or 0 when none.</param>
/// <param name="Offset">The byte offset involved, or -1 when none.</param>
[DebuggerDisplay("{Code} object {ObjectNumber} at {Offset}")]
public readonly record struct PdfDiagnostic(PdfDiagnosticCode Code, string Message, int ObjectNumber, long Offset);
