// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>One entry of a JP2 component mapping box (I.5.3.5): where an output channel comes from.</summary>
/// <param name="Component">The codestream component.</param>
/// <param name="ThroughPalette">Whether the component is a palette index rather than the value itself.</param>
/// <param name="Column">The palette column.</param>
internal readonly record struct JpxChannelMapping(int Component, bool ThroughPalette, int Column);
