// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>One progression volume: an order and the layer, resolution and component ranges it covers.</summary>
/// <param name="ResolutionStart">The first resolution.</param>
/// <param name="ComponentStart">The first component.</param>
/// <param name="LayerEnd">The layer after the last.</param>
/// <param name="ResolutionEnd">The resolution after the last.</param>
/// <param name="ComponentEnd">The component after the last.</param>
/// <param name="Order">The packet order within the volume.</param>
internal readonly record struct JpxProgressionChange(int ResolutionStart, int ComponentStart, int LayerEnd, int ResolutionEnd, int ComponentEnd, JpxProgressionOrder Order);
