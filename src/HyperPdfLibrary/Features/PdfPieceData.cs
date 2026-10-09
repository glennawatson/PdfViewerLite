// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Features;

/// <summary>One application's entry in a page-piece dictionary (<c>/PieceInfo</c>).</summary>
/// <param name="Name">The application's name (the dictionary key).</param>
/// <param name="LastModified">When the application last changed its data, or null.</param>
/// <param name="Data">The data dictionary, or null.</param>
[DebuggerDisplay("PdfPieceData: {Name}")]
public sealed record PdfPieceData(string Name, DateTimeOffset? LastModified, PdfDictionary? Data);
