// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>One entry of a JP2 channel definition box (I.5.3.6).</summary>
/// <param name="Channel">The channel index.</param>
/// <param name="Type">0 for colour, 1 for opacity, 2 for premultiplied opacity.</param>
/// <param name="Association">The colour the channel belongs to, from 1; 0 or 65535 for the whole image.</param>
internal readonly record struct JpxChannelDefinition(int Channel, int Type, int Association);
