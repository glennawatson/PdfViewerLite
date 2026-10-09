// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Navigation;

/// <summary>Plays, pauses, stops or resumes a movie annotation.</summary>
/// <param name="Title">The title of the target annotation (<c>/T</c>), or null.</param>
/// <param name="Annotation">The target annotation dictionary (<c>/Annotation</c>), or null.</param>
/// <param name="Operation">The operation: Play, Stop, Pause or Resume.</param>
[DebuggerDisplay("MovieAction: {Operation} {Title}")]
public sealed record MovieAction(string? Title, PdfDictionary? Annotation, string Operation);
