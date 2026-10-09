// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Extensions;

/// <summary>
/// A dictionary key that ISO 32000-2 does not define for its dictionary. An unknown key is not an error; the
/// flags tell an unsupported extension (a well-formed value, often a declared or prefixed key) from damage.
/// </summary>
/// <param name="Owner">The kind of dictionary.</param>
/// <param name="PageIndex">The zero based page for <see cref="PdfEntryOwner.Page"/>, otherwise null.</param>
/// <param name="Key">The key, without the slash.</param>
/// <param name="ValueKind">The kind of the value after following references.</param>
/// <param name="ParsedCleanly">Whether the value, and every object it refers to within a bounded depth, was read without a missing or unreadable object.</param>
/// <param name="HasDeveloperPrefix">Whether the key has a second-class name prefix such as <c>ADBE_</c>.</param>
/// <param name="IsDeclared">Whether the key's prefix is declared in the catalog's <c>/Extensions</c>.</param>
[DebuggerDisplay("PdfUnknownEntry: {Owner} /{Key} clean={ParsedCleanly}")]
public sealed record PdfUnknownEntry(PdfEntryOwner Owner, int? PageIndex, string Key, PdfKind ValueKind, bool ParsedCleanly, bool HasDeveloperPrefix, bool IsDeclared);
