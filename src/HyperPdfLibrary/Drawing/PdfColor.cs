// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Drawing;

/// <summary>An 8-bit red, green, blue and alpha colour.</summary>
/// <param name="Red">The red channel.</param>
/// <param name="Green">The green channel.</param>
/// <param name="Blue">The blue channel.</param>
/// <param name="Alpha">The alpha channel.</param>
[DebuggerDisplay("PdfColor: {Red:X2}{Green:X2}{Blue:X2}{Alpha:X2}")]
public readonly record struct PdfColor(byte Red, byte Green, byte Blue, byte Alpha)
{
    /// <summary>Initializes a new instance of the <see cref="PdfColor"/> class.</summary>
    /// <param name="red">The red channel.</param>
    /// <param name="green">The green channel.</param>
    /// <param name="blue">The blue channel.</param>
    public PdfColor(byte red, byte green, byte blue)
        : this(red, green, blue, byte.MaxValue)
    {
    }

    /// <summary>Gets a fully transparent colour.</summary>
    public static PdfColor Transparent => default;

    /// <summary>Gets an opaque black colour.</summary>
    public static PdfColor Black => new(0, 0, 0, byte.MaxValue);

    /// <summary>Gets an opaque white colour.</summary>
    public static PdfColor White => new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
}
