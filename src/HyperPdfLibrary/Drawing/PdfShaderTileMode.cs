// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Drawing;

/// <summary>How a shader paints outside its source.</summary>
public enum PdfShaderTileMode
{
    /// <summary>Use the nearest edge.</summary>
    Clamp = 0,
    /// <summary>Repeat the source.</summary>
    Repeat = 1,
    /// <summary>Reflect alternate repetitions.</summary>
    Mirror = 2,
    /// <summary>Paint transparent pixels.</summary>
    Decal = 3,
}
