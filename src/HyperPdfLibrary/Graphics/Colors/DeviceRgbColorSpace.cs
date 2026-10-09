// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>The DeviceRGB colour space; one shared instance.</summary>
internal sealed class DeviceRgbColorSpace : PdfColorSpace
{
    /// <summary>Initializes a new instance of the <see cref="DeviceRgbColorSpace"/> class.</summary>
    private DeviceRgbColorSpace()
    {
    }

    /// <inheritdoc/>
    public override int Components => RgbComponents;

    /// <inheritdoc/>
    public override PdfColorSpaceKind Kind => PdfColorSpaceKind.DeviceRgb;

    /// <summary>Gets the shared instance.</summary>
    internal static DeviceRgbColorSpace Instance { get; } = new();

    /// <inheritdoc/>
    private protected override void ToRgbCore(ReadOnlySpan<float> components, Span<float> rgb)
    {
        rgb[0] = Math.Clamp(components[0], 0, 1);
        rgb[1] = Math.Clamp(components[1], 0, 1);
        rgb[2] = Math.Clamp(components[2], 0, 1);
    }

    /// <inheritdoc/>
    private protected override void ConvertRowCore(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount) =>
        PixelConverter.RgbToBgra(samples, bgra, pixelCount);
}
