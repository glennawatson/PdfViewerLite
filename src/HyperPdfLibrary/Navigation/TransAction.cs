// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Features;

namespace HyperPdfLibrary.Navigation;

/// <summary>Updates the display with a transition effect.</summary>
/// <param name="Transition">The transition.</param>
[DebuggerDisplay("TransAction: {Transition}")]
public sealed record TransAction(PdfPageTransition Transition);
