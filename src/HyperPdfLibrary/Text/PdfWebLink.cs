// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Text;

/// <summary>A web address or email address written as plain text on a page.</summary>
/// <param name="Url">The address, with <c>http://</c> added to bare <c>www.</c> hosts and <c>mailto:</c> to email addresses.</param>
/// <param name="Start">The first character index.</param>
/// <param name="Count">The number of characters.</param>
[DebuggerDisplay("PdfWebLink: {Url}")]
public sealed record PdfWebLink(string Url, int Start, int Count);
