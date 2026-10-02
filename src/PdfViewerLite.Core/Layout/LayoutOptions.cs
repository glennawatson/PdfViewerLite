// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.Core.Layout;

/// <summary>Inputs to <see cref="DocumentLayout.Create"/>.</summary>
/// <param name="Rotation">The page rotation.</param>
/// <param name="Mode">The page arrangement.</param>
/// <param name="Scale">Device independent pixels per point.</param>
/// <param name="Spacing">The gap between pages.</param>
/// <param name="Margin">The margin around the content.</param>
/// <param name="ViewportWidth">The viewport width, used to centre narrow content.</param>
[DebuggerDisplay("{Mode} x{Scale}")]
public readonly record struct LayoutOptions(PageRotation Rotation, PageLayoutMode Mode, double Scale, double Spacing, double Margin, double ViewportWidth);
