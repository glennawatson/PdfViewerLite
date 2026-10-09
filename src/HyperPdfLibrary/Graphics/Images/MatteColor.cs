// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>The RGB form of a soft mask's /Matte colour.</summary>
/// <param name="Red">The red byte.</param>
/// <param name="Green">The green byte.</param>
/// <param name="Blue">The blue byte.</param>
internal readonly record struct MatteColor(byte Red, byte Green, byte Blue);
