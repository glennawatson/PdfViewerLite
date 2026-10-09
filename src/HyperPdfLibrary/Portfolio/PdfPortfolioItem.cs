// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Portfolio;

/// <summary>An embedded file as a portfolio lists it.</summary>
/// <param name="AttachmentIndex">The file's index in the document's attachment list (<c>GetAttachments</c>).</param>
/// <param name="FileName">The file name.</param>
/// <param name="FolderId">The id of the folder holding the file, or null when the file's name-tree key has no <c>&lt;n&gt;</c> prefix (the file is in the root folder).</param>
/// <param name="Fields">The collection item values by schema key, as text. A sub-item shows its data.</param>
[DebuggerDisplay("PdfPortfolioItem: {FileName}")]
public sealed record PdfPortfolioItem(int AttachmentIndex, string FileName, int? FolderId, IReadOnlyDictionary<string, string> Fields);
