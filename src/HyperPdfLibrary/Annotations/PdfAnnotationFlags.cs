// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Annotations;

/// <summary>The annotation flags of an annotation's <c>/F</c> entry (PDF 32000-2, 12.5.3).</summary>
[Flags]
public enum PdfAnnotationFlags
{
    /// <summary>No flags.</summary>
    None = 0,

    /// <summary>An unknown annotation type is not shown.</summary>
    Invisible = 1 << 0,

    /// <summary>The annotation is neither shown nor printed.</summary>
    Hidden = 1 << 1,

    /// <summary>The annotation is printed.</summary>
    Print = 1 << 2,

    /// <summary>The appearance does not scale with zoom.</summary>
    NoZoom = 1 << 3,

    /// <summary>The appearance does not rotate with the page.</summary>
    NoRotate = 1 << 4,

    /// <summary>The annotation is not shown on screen.</summary>
    NoView = 1 << 5,

    /// <summary>The annotation does not respond to the user.</summary>
    ReadOnly = 1 << 6,

    /// <summary>The annotation cannot be moved or deleted.</summary>
    Locked = 1 << 7,

    /// <summary>The <see cref="NoView"/> flag is toggled on hover and selection.</summary>
    ToggleNoView = 1 << 8,

    /// <summary>The annotation's contents cannot be changed.</summary>
    LockedContents = 1 << 9,
}
