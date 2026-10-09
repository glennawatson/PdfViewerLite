// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Redaction;

/// <summary>A redact annotation being applied: what it says and the dictionary it came from.</summary>
/// <param name="Info">The areas and appearance.</param>
/// <param name="Annotation">The annotation dictionary, which may hold an overlay form.</param>
[DebuggerDisplay("RedactMark: annotation {Info.AnnotationIndex}")]
internal sealed record RedactMark(PdfRedaction Info, PdfDictionary Annotation);
