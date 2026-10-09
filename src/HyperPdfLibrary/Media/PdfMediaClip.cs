// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Media;

/// <summary>A media clip: the media data, or a section of another clip.</summary>
/// <param name="Subtype">MCD (clip data) or MCS (clip section).</param>
/// <param name="Name">The clip name (<c>/N</c>), or null.</param>
/// <param name="FileName">The file name when the data is an external file specification, or null.</param>
/// <param name="ContentType">The MIME type (<c>/CT</c>), or null.</param>
/// <param name="Data">The embedded media stream, or null when the data is external.</param>
/// <param name="Permission">The <c>/P /TF</c> temporary file permission (TEMPNEVER, TEMPEXTRACT, TEMPACCESS or TEMPALWAYS), or null.</param>
/// <param name="Section">For a clip section, the clip it is a section of; otherwise null.</param>
[DebuggerDisplay("PdfMediaClip: {Subtype} {Name}")]
public sealed record PdfMediaClip(string Subtype, string? Name, string? FileName, string? ContentType, PdfStream? Data, string? Permission, PdfMediaClip? Section);
