// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Interchange;

/// <summary>The file a file attachment annotation carries.</summary>
/// <param name="FileName">The file's name.</param>
/// <param name="Data">The file's bytes, decoded.</param>
/// <param name="Description">The description, or <see langword="null"/>.</param>
/// <param name="MimeType">The media type, or <see langword="null"/>.</param>
[DebuggerDisplay("PdfInterchangeAttachment: {FileName} {Data.Length} bytes")]
public sealed record PdfInterchangeAttachment(string FileName, byte[] Data, string? Description, string? MimeType);
