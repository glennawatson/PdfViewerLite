// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media;

/// <summary>A 3D annotation.</summary>
/// <param name="Stream">The 3D data and its views, or null when the annotation has none.</param>
/// <param name="Activation">When it activates, or null for the defaults.</param>
/// <param name="InitialView">The view the annotation shows first (<c>/3DV</c> as a view dictionary), or null.</param>
/// <param name="InitialViewSelector">The first view when <c>/3DV</c> is an index, name or string, or null.</param>
[DebuggerDisplay("Pdf3DAnnotation: {Stream}")]
public sealed record Pdf3DAnnotation(Pdf3DStream? Stream, Pdf3DActivation? Activation, Pdf3DView? InitialView, string? InitialViewSelector);
