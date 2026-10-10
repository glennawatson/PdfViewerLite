// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Rendering;

/// <summary>Describes a soft mask layer opened around a painting operation.</summary>
/// <param name="Active">Whether a layer was opened.</param>
/// <param name="Bounds">The layer's area in page space.</param>
[DebuggerDisplay("MaskScope: active {Active}")]
internal readonly record struct MaskScope(bool Active, PdfRect Bounds);
