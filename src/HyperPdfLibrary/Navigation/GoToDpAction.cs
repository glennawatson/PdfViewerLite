// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Navigation;

/// <summary>Jumps to a document part (PDF 2.0).</summary>
/// <param name="Part">The document part node dictionary (<c>/Dp</c>), or null.</param>
[DebuggerDisplay("GoToDpAction")]
public sealed record GoToDpAction(PdfDictionary? Part);
