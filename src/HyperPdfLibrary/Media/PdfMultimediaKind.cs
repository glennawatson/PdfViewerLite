// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Media;

/// <summary>The kind of a multimedia annotation.</summary>
public enum PdfMultimediaKind
{
    /// <summary>A screen annotation.</summary>
    Screen = 0,

    /// <summary>A movie annotation.</summary>
    Movie = 1,

    /// <summary>A sound annotation.</summary>
    Sound = 2,

    /// <summary>A rich media annotation.</summary>
    RichMedia = 3,

    /// <summary>A 3D annotation.</summary>
    ThreeD = 4,
}
