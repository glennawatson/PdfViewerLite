// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>A resource the regenerated content names that is not in the resources yet; it is stored when the content is applied.</summary>
/// <param name="Category">The resource category, such as /XObject or /Font.</param>
/// <param name="Name">The name the content uses.</param>
/// <param name="Value">The resource: a stream or dictionary to store, or a reference.</param>
/// <param name="Form">A changed form's content, written as a new form XObject, or <see langword="null"/>.</param>
[DebuggerDisplay("PendingResource: {Category}")]
internal sealed record PendingResource(KnownName Category, PdfName Name, PdfValue Value, PdfPageContent? Form);
