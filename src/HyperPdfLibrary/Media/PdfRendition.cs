// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Media;

/// <summary>A rendition: how a media clip is played.</summary>
/// <param name="Subtype">MR (media rendition) or SR (selector rendition).</param>
/// <param name="Name">The rendition name (<c>/N</c>), or null.</param>
/// <param name="Clip">The media clip of a media rendition, or null.</param>
/// <param name="PlayParameters">The play parameters (<c>/P</c>), or null.</param>
/// <param name="ScreenParameters">The screen parameters (<c>/SP</c>), or null.</param>
/// <param name="MustHonor">The must-honour criteria (<c>/MH</c>), or null.</param>
/// <param name="BestEffort">The best-effort criteria (<c>/BE</c>), or null.</param>
[DebuggerDisplay("PdfRendition: {Subtype} {Name}")]
public sealed record PdfRendition(
    string Subtype,
    string? Name,
    PdfMediaClip? Clip,
    PdfDictionary? PlayParameters,
    PdfDictionary? ScreenParameters,
    PdfDictionary? MustHonor,
    PdfDictionary? BestEffort);
