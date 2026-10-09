// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>The name tables of two revisions being compared.</summary>
/// <param name="Left">The first revision's table.</param>
/// <param name="Right">The second revision's table.</param>
[DebuggerDisplay("NameTables")]
internal readonly record struct NameTables(PdfNameTable Left, PdfNameTable Right);
