// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>One resource name a content stream uses.</summary>
/// <param name="Category">The resource category, such as /XObject or /Font.</param>
/// <param name="Name">The resource name.</param>
[DebuggerDisplay("ResourceUse: {Category} {Name}")]
internal readonly record struct ResourceUse(KnownName Category, PdfName Name);
