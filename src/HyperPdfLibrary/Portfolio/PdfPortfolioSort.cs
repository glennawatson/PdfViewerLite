// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Portfolio;

/// <summary>One sort key of a portfolio.</summary>
/// <param name="Key">The schema field to sort by.</param>
/// <param name="Ascending">Whether the order is ascending.</param>
[DebuggerDisplay("PdfPortfolioSort: {Key} ascending={Ascending}")]
public readonly record struct PdfPortfolioSort(string Key, bool Ascending);
