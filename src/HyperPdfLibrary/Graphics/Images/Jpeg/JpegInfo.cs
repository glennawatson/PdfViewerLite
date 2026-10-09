// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>What the headers of a JPEG say about its size, coding and colour transform.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels; zero when the frame header defers it to a DNL marker.</param>
/// <param name="Components">The number of colour components.</param>
/// <param name="Precision">The bits per sample.</param>
/// <param name="Process">The coding process.</param>
/// <param name="AdobeTransform">The transform byte of the Adobe APP14 marker, or -1 when the marker is missing.</param>
/// <param name="HasRgbIds">Whether three components carry the ASCII ids R, G and B.</param>
internal readonly record struct JpegInfo(
    int Width,
    int Height,
    int Components,
    int Precision,
    JpegProcess Process,
    int AdobeTransform,
    bool HasRgbIds)
{
    /// <summary>The value of <c>/ColorTransform</c> when the decode parameters do not give one.</summary>
    internal const int NoColorTransform = -1;

    /// <summary>The components of a gray JPEG.</summary>
    private const int GrayComponents = 1;

    /// <summary>The components of a YCbCr or RGB JPEG.</summary>
    private const int ThreeComponents = 3;

    /// <summary>The components of a CMYK or YCCK JPEG.</summary>
    private const int FourComponents = 4;

    /// <summary>The only sample depth the managed decoder reads.</summary>
    private const int SupportedPrecision = 8;

    /// <summary>Gets a value indicating whether the Adobe APP14 marker is present.</summary>
    internal bool HasAdobe => AdobeTransform >= 0;

    /// <summary>Gets a value indicating whether the managed decoder can read this JPEG.</summary>
    internal bool IsSupported =>
        Process != JpegProcess.Unsupported
        && Precision == SupportedPrecision
        && Width > 0
        && Height > 0
        && Components is GrayComponents or ThreeComponents or FourComponents;

    /// <summary>
    /// Decides whether the samples are YCbCr (three components) or YCCK (four) and must be converted to RGB or CMYK.
    /// The Adobe marker wins; otherwise the PDF <c>/ColorTransform</c> entry decides, and RGB-labelled components are left alone.
    /// </summary>
    /// <param name="colorTransform">The <c>/ColorTransform</c> value, or <see cref="NoColorTransform"/>.</param>
    /// <returns><see langword="true"/> when the colour transform applies.</returns>
    internal bool UsesColorTransform(int colorTransform) => Components switch
    {
        ThreeComponents => HasAdobe ? AdobeTransform != 0 : colorTransform != 0 && !HasRgbIds,
        FourComponents => HasAdobe ? AdobeTransform != 0 : colorTransform == 1,
        _ => false,
    };
}
