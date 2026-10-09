// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Media;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Navigation;

/// <summary>Sets the current view of a 3D annotation.</summary>
/// <param name="Target">The 3D annotation dictionary (<c>/TA</c>), or null.</param>
/// <param name="View">The view when <c>/V</c> is a view dictionary, or null.</param>
/// <param name="ViewSelector">The view selector when <c>/V</c> is a name (F, L, N, P, D), an index or an external name; null when <c>/V</c> is a dictionary.</param>
/// <param name="Command">The <c>/C</c> name: Linear or Instantaneous; null when missing.</param>
[DebuggerDisplay("GoTo3DViewAction: {ViewSelector}")]
public sealed record GoTo3DViewAction(PdfDictionary? Target, Pdf3DView? View, string? ViewSelector, string? Command);
