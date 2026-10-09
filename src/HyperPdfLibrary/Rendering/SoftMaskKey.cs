// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>Identifies a recorded soft mask: its dictionary and the transform it was recorded with.</summary>
/// <param name="Mask">The soft mask dictionary.</param>
/// <param name="Ctm">The matrix from user space to the page when the mask was set.</param>
[DebuggerDisplay("SoftMaskKey: {Ctm}")]
internal readonly record struct SoftMaskKey(PdfDictionary Mask, Matrix3x2 Ctm);
