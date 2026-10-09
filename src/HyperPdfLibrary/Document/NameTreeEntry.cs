// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>One key and value pair of a name or number tree.</summary>
/// <param name="Key">The key.</param>
/// <param name="Value">The value.</param>
[DebuggerDisplay("NameTreeEntry: {Key}")]
internal readonly record struct NameTreeEntry(PdfValue Key, PdfValue Value);
