// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The order packets appear in (ISO 15444-1 table A.16).</summary>
internal enum JpxProgressionOrder
{
    /// <summary>Layer, resolution, component, position.</summary>
    LayerResolutionComponentPosition = 0,

    /// <summary>Resolution, layer, component, position.</summary>
    ResolutionLayerComponentPosition = 1,

    /// <summary>Resolution, position, component, layer.</summary>
    ResolutionPositionComponentLayer = 2,

    /// <summary>Position, component, resolution, layer.</summary>
    PositionComponentResolutionLayer = 3,

    /// <summary>Component, position, resolution, layer.</summary>
    ComponentPositionResolutionLayer = 4,
}
