// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Content;

/// <summary>
/// One operand of a content stream operator. Numbers and names are held inline; strings, arrays and dictionaries are
/// byte ranges of the content, read only when an operator needs them, so collecting operands never allocates.
/// </summary>
/// <param name="Kind">The kind of operand.</param>
/// <param name="Number">The number, for numbers and booleans (1 or 0).</param>
/// <param name="Name">The name, for names.</param>
/// <param name="Start">The first byte of a string's, array's or dictionary's body in the content.</param>
/// <param name="Length">The length of the body.</param>
[DebuggerDisplay("ContentOperand: {Kind} {Number}")]
public readonly record struct ContentOperand(ContentOperandKind Kind, float Number, PdfName Name, int Start, int Length);
