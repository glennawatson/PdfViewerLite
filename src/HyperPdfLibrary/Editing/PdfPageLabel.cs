// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>A page's label as a style and a number, so it can be kept when the page moves.</summary>
/// <param name="Style">The label range dictionary (/S, /P, /St) the page falls in, or <see langword="null"/> for none.</param>
/// <param name="Number">The page's number within its style.</param>
[DebuggerDisplay("PdfPageLabel: {Number}")]
internal readonly record struct PdfPageLabel(PdfDictionary? Style, int Number);
