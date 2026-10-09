// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>The widest empty band across a region of lines, where the layout reading order may cut it.</summary>
/// <param name="Position">The band's middle.</param>
/// <param name="Width">The band's width; zero when there is none.</param>
[DebuggerDisplay("LayoutGap: {Width} at {Position}")]
internal readonly record struct LayoutGap(float Position, float Width);
