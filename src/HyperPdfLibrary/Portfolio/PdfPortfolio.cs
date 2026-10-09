// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Portfolio;

/// <summary>A PDF portfolio (collection): how the embedded files should be presented.</summary>
/// <param name="Schema">The fields (columns), in the order of their <c>/O</c> values.</param>
/// <param name="Sort">The sort keys.</param>
/// <param name="InitialDocument">The name of the embedded file to show first, or null.</param>
/// <param name="View">The initial view: D (details), T (tile) or H (hidden).</param>
/// <param name="Colors">The requested colours, or null.</param>
/// <param name="RootFolder">The root of the folder tree, or null when the portfolio has no folders.</param>
/// <param name="Items">The embedded files, in the order of the document's attachment list.</param>
[DebuggerDisplay("PdfPortfolio: {Items.Count} files, view {View}")]
public sealed record PdfPortfolio(
    IReadOnlyList<PdfPortfolioField> Schema,
    IReadOnlyList<PdfPortfolioSort> Sort,
    string? InitialDocument,
    string View,
    PdfPortfolioColors? Colors,
    PdfPortfolioFolder? RootFolder,
    IReadOnlyList<PdfPortfolioItem> Items);
