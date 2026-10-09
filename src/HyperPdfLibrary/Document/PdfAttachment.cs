// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>A file embedded in the document.</summary>
/// <param name="Index">The attachment's position in the /EmbeddedFiles name tree.</param>
/// <param name="Name">The file name, from the file specification.</param>
/// <param name="Data">The embedded file stream, or <see langword="null"/> when the specification has none.</param>
[DebuggerDisplay("PdfAttachment: {Name}")]
public sealed record PdfAttachment(int Index, string Name, PdfStream? Data);
