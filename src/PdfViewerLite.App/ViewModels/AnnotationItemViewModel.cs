// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using PdfViewerLite.Core.Annotations;

namespace PdfViewerLite.App.ViewModels;

/// <summary>An annotation listed in the sidebar.</summary>
/// <param name="Annotation">The annotation.</param>
/// <param name="PageLabel">The page label shown, for example "iv" or "12".</param>
[DebuggerDisplay("{Summary}")]
public sealed record AnnotationItemViewModel(PageAnnotation Annotation, string PageLabel)
{
    /// <summary>Gets the kind in words, so colour is never the only cue.</summary>
    public string KindName => AnnotationNames.Get(Annotation.Kind);

    /// <summary>Gets the line shown under the kind: the note, or the page.</summary>
    public string Summary => Annotation.Contents.Length > 0 ? Annotation.Contents : string.Create(CultureInfo.CurrentCulture, $"Page {PageLabel}");

    /// <summary>Gets the page caption.</summary>
    public string PageCaption => string.Create(CultureInfo.CurrentCulture, $"Page {PageLabel}");
}
