// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Portfolio;

/// <summary>A column of a portfolio's schema.</summary>
/// <param name="Key">The key that collection items use for this field.</param>
/// <param name="Subtype">The field type: S (string), D (date), N (number), or a file property such as F, Desc, ModDate, CreationDate, Size or CompressedSize.</param>
/// <param name="Name">The name shown to people.</param>
/// <param name="Order">The position among the fields.</param>
/// <param name="Visible">Whether the field is shown.</param>
/// <param name="Editable">Whether people may edit the field.</param>
[DebuggerDisplay("PdfPortfolioField: {Key} {Subtype}")]
public sealed record PdfPortfolioField(string Key, string Subtype, string Name, int Order, bool Visible, bool Editable);
