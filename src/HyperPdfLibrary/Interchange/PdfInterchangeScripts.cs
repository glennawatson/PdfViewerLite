// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Interchange;

/// <summary>The JavaScript of an FDF file (ISO 32000-1, 12.7.7.3.2), kept as data. The library never runs it.</summary>
/// <param name="Before">The script to run before the data is imported, or <see langword="null"/>.</param>
/// <param name="After">The script to run after the data is imported, or <see langword="null"/>.</param>
/// <param name="AfterPermsReady">The script to run once permissions are known, or <see langword="null"/>.</param>
/// <param name="Document">The named document-level scripts as name and script pairs, flattened: name, script, name, script.</param>
[DebuggerDisplay("PdfInterchangeScripts: {Document.Length} document scripts")]
public sealed record PdfInterchangeScripts(string? Before, string? After, string? AfterPermsReady, string[] Document);
