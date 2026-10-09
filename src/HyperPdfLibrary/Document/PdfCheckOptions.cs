// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>Options for a whole-document check with <see cref="PdfDocumentCheck.Check(PdfDocument, PdfCheckOptions)"/>.</summary>
[DebuggerDisplay("PdfCheckOptions: streams {DecodeStreams} content {ParseContent} recovery {Recovery}")]
public sealed record PdfCheckOptions
{
    /// <summary>Gets the shared options that check everything and allow recovery.</summary>
    public static PdfCheckOptions Default { get; } = new();

    /// <summary>Gets or sets a value indicating whether every stream is decoded to find truncated or damaged data; <see langword="true"/> by default.</summary>
    public bool DecodeStreams { get; init; } = true;

    /// <summary>Gets or sets a value indicating whether every page's content stream is parsed to find unknown operators and unbalanced state; <see langword="true"/> by default.</summary>
    public bool ParseContent { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether a damaged cross-reference table may be rebuilt while opening a file for
    /// the check; <see langword="true"/> by default. Set it to <see langword="false"/> to see the file as a strict reader
    /// does: a file that needs rebuilding then reports one fault instead of being repaired. It applies only to the
    /// static check methods, which open the file; a document that is already open keeps the options it was opened with.
    /// </summary>
    public bool Recovery { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether cross-reference streams are ignored while opening a file for the check, so
    /// a file that needs them reads as damaged. It applies only to the static check methods.
    /// </summary>
    public bool IgnoreXrefStreams { get; init; }
}
