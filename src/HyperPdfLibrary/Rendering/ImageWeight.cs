// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Rendering;

/// <summary>One image a recorded picture drew: which image, and the pixel memory it holds.</summary>
/// <param name="Id">The Skia unique id of the image, which every picture that draws the same image shares.</param>
/// <param name="Bytes">The pixel bytes of the image.</param>
[DebuggerDisplay("ImageWeight: image {Id}, {Bytes} bytes")]
internal readonly record struct ImageWeight(uint Id, long Bytes);
