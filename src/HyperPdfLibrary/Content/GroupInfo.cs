// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;

namespace HyperPdfLibrary.Content;

/// <summary>A transparency group the interpreter asks a device to composite as one object.</summary>
/// <param name="Isolated">Whether the group is isolated from its backdrop.</param>
/// <param name="Knockout">Whether objects in the group knock out earlier ones.</param>
/// <param name="Alpha">The constant alpha the group is composited with.</param>
/// <param name="Blend">The blend mode the group is composited with.</param>
/// <param name="SoftMask">The soft mask applied to the group, or null.</param>
/// <param name="Bounds">The area the group can paint, in page space.</param>
[DebuggerDisplay("GroupInfo: alpha {Alpha} {Blend}")]
public readonly record struct GroupInfo(bool Isolated, bool Knockout, float Alpha, PdfBlendMode Blend, PdfSoftMask? SoftMask, PdfRect Bounds);
