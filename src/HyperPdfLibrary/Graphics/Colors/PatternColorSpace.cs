// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// The Pattern colour space. Colour components belong to the underlying space of an uncoloured pattern; without one there
/// are none and colours convert to black, since the pattern itself supplies the paint.
/// </summary>
internal sealed class PatternColorSpace : PdfColorSpace
{
    /// <summary>Initializes a new instance of the <see cref="PatternColorSpace"/> class.</summary>
    /// <param name="underlying">The underlying space of uncoloured patterns, or <see langword="null"/>.</param>
    internal PatternColorSpace(PdfColorSpace? underlying) => Underlying = underlying;

    /// <inheritdoc/>
    public override int Components => Underlying?.Components ?? 0;

    /// <inheritdoc/>
    public override PdfColorSpaceKind Kind => PdfColorSpaceKind.Pattern;

    /// <summary>Gets the shared Pattern space with no underlying space.</summary>
    internal static PatternColorSpace Colored { get; } = new(null);

    /// <summary>Gets the underlying space of uncoloured patterns, or <see langword="null"/>.</summary>
    internal PdfColorSpace? Underlying { get; }

    /// <inheritdoc/>
    public override void GetInitialColor(Span<float> components) => Underlying?.GetInitialColor(components);

    /// <inheritdoc/>
    public override float[] GetDefaultDecode(int bitsPerComponent) => Underlying?.GetDefaultDecode(bitsPerComponent) ?? [];

    /// <inheritdoc/>
    private protected override void ToRgbCore(ReadOnlySpan<float> components, Span<float> rgb)
    {
        if (Underlying is null)
        {
            rgb[..RgbComponents].Clear();
            return;
        }

        Underlying.ToRgb(components, rgb);
    }

    /// <inheritdoc/>
    private protected override void ConvertRowCore(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount)
    {
        if (Underlying is null)
        {
            ConvertRowScalar(samples, bgra, pixelCount);
            return;
        }

        Underlying.ConvertRow(samples, bgra, pixelCount);
    }
}
