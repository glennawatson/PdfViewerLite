// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Navigation;

/// <summary>Jumps to an article thread, and optionally to a bead of it.</summary>
/// <param name="File">The file holding the thread, or null for this document.</param>
/// <param name="Thread">The thread dictionary when <c>/D</c> is one, or null.</param>
/// <param name="ThreadIndex">The thread's index in the catalog's <c>/Threads</c> when <c>/D</c> is an integer, or null.</param>
/// <param name="ThreadTitle">The thread's title when <c>/D</c> is a string, or null.</param>
/// <param name="BeadIndex">The bead's index within the thread when <c>/B</c> is an integer, or null.</param>
[DebuggerDisplay("ThreadAction: {ThreadTitle}")]
public sealed record ThreadAction(string? File, PdfDictionary? Thread, int? ThreadIndex, string? ThreadTitle, int? BeadIndex);
