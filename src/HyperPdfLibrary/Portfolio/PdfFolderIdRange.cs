// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Portfolio;

/// <summary>A range of folder ids that a folder keeps free for new child folders.</summary>
/// <param name="First">The first free id.</param>
/// <param name="Last">The last free id.</param>
[DebuggerDisplay("PdfFolderIdRange: {First}-{Last}")]
public readonly record struct PdfFolderIdRange(int First, int Last);
