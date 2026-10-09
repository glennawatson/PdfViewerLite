// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>Annotation content that needs a player a page viewer does not have.</summary>
[Flags]
public enum PdfAnnotationContent
{
    /// <summary>Nothing that needs a player.</summary>
    None = 0,

    /// <summary>A sound, movie, screen (media player) or rich media annotation.</summary>
    Multimedia = 1 << 0,

    /// <summary>A 3D annotation.</summary>
    ThreeD = 1 << 1,
}
