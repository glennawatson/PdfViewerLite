// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>A component as the frame header declares it.</summary>
/// <param name="Id">The component id.</param>
/// <param name="HorizontalSampling">The horizontal sampling factor.</param>
/// <param name="VerticalSampling">The vertical sampling factor.</param>
/// <param name="QuantId">The quantization table id.</param>
internal readonly record struct JpegComponentSpec(int Id, int HorizontalSampling, int VerticalSampling, int QuantId);
