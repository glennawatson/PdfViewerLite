// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>The DeviceGray colour space; one shared instance.</summary>
internal sealed class DeviceGrayColorSpace : PdfColorSpace
{
    /// <summary>Initializes a new instance of the <see cref="DeviceGrayColorSpace"/> class.</summary>
    private DeviceGrayColorSpace()
    {
    }

    /// <inheritdoc/>
    public override int Components => 1;

    /// <inheritdoc/>
    public override PdfColorSpaceKind Kind => PdfColorSpaceKind.DeviceGray;

    /// <summary>Gets the shared instance.</summary>
    internal static DeviceGrayColorSpace Instance { get; } = new();

    /// <inheritdoc/>
    private protected override void ToRgbCore(ReadOnlySpan<float> components, Span<float> rgb) => rgb[..RgbComponents].Fill(Math.Clamp(components[0], 0, 1));

    /// <inheritdoc/>
    private protected override void ConvertRowCore(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount) =>
        PixelConverter.GrayToBgra(samples, bgra, pixelCount);
}
