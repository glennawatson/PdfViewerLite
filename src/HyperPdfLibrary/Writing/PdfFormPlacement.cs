// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>A form XObject drawn on a sheet.</summary>
/// <param name="Form">The form XObject in the document being built.</param>
/// <param name="Transform">Maps the form's space to the sheet's space, in PDF's <c>a b c d e f</c> order.</param>
[DebuggerDisplay("PdfFormPlacement: {Form}")]
public readonly record struct PdfFormPlacement(PdfObjectId Form, Matrix3x2 Transform);
