// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Builds image XObjects from pixels: Flate-compressed, 8 bits per component, with an optional soft mask.</summary>
internal static class ImageStreams
{
    /// <summary>The bits per component written.</summary>
    private const int EightBits = 8;

    /// <summary>The bytes per pixel of a DeviceGray image.</summary>
    private const int GrayBytes = 1;

    /// <summary>The bytes per pixel of a DeviceRGB image.</summary>
    private const int RgbBytes = 3;

    /// <summary>The entries of an image dictionary.</summary>
    private const int ImageEntries = 8;

    /// <summary>Creates an image XObject.</summary>
    /// <param name="owner">The document the stream belongs to, or <see langword="null"/>.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="format">The layout of the pixels.</param>
    /// <param name="pixels">The pixels, with no row padding.</param>
    /// <param name="alpha">One opacity byte per pixel, or empty for an opaque image.</param>
    /// <returns>The image stream; a soft mask is held directly in its dictionary until <see cref="Promote"/> adds it to a store.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A size is not positive.</exception>
    /// <exception cref="ArgumentException">A buffer is not the length the size and format need.</exception>
    internal static PdfStream Create(PdfObjectStore? owner, int width, int height, PdfImagePixelFormat format, ReadOnlySpan<byte> pixels, ReadOnlySpan<byte> alpha)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var perPixel = format == PdfImagePixelFormat.Rgb24 ? RgbBytes : GrayBytes;
        var count = checked(width * height);
        if (pixels.Length != checked(count * perPixel))
        {
            throw new ArgumentException("The pixels do not match the size and format.", nameof(pixels));
        }

        if (!alpha.IsEmpty && alpha.Length != count)
        {
            throw new ArgumentException("The alpha does not match the size.", nameof(alpha));
        }

        var space = format == PdfImagePixelFormat.Rgb24 ? KnownName.DeviceRGB : KnownName.DeviceGray;
        var image = CreateStream(owner, width, height, EightBits, pixels, PdfValue.FromName(space));
        if (!alpha.IsEmpty)
        {
            var mask = CreateStream(owner, width, height, EightBits, alpha, PdfValue.FromName(KnownName.DeviceGray));
            image.Dictionary.Set(KnownName.SMask, PdfValue.FromStream(mask));
        }

        return image;
    }

    /// <summary>Creates a 1-bit stencil mask image XObject.</summary>
    /// <param name="owner">The document the stream belongs to, or <see langword="null"/>.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="bits">The samples, rows padded to whole bytes.</param>
    /// <param name="decode">The /Decode entries of the original image, or <see langword="null"/> for the default.</param>
    /// <returns>The stream.</returns>
    internal static PdfStream CreateStencil(PdfObjectStore? owner, int width, int height, ReadOnlySpan<byte> bits, PdfArray? decode)
    {
        var image = CreateStream(owner, width, height, 1, bits, default);
        image.Dictionary.Set(KnownName.ImageMask, PdfValue.FromBoolean(true));
        if (decode is not null)
        {
            image.Dictionary.Set(KnownName.Decode, PdfValue.FromArray(decode));
        }

        return image;
    }

    /// <summary>Adds a soft mask held directly in an image's dictionary to the store, so it is written as an indirect object.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="image">The image stream.</param>
    internal static void Promote(PdfObjectStore store, PdfStream image)
    {
        if (image.Dictionary.GetRaw(KnownName.SMask).AsStream() is { } mask)
        {
            image.Dictionary.Set(KnownName.SMask, PdfValue.FromReference(store.Add(PdfValue.FromStream(mask))));
        }
    }

    /// <summary>Builds an image stream, Flate-compressed.</summary>
    /// <param name="owner">The document.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="bits">The bits per component.</param>
    /// <param name="samples">The sample bytes.</param>
    /// <param name="colorSpace">The colour space, or null for a stencil mask.</param>
    /// <returns>The stream.</returns>
    private static PdfStream CreateStream(PdfObjectStore? owner, int width, int height, int bits, ReadOnlySpan<byte> samples, PdfValue colorSpace)
    {
        var dictionary = new PdfDictionary(owner, ImageEntries);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.XObject));
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Image));
        dictionary.Set(KnownName.Width, PdfValue.FromInteger(width));
        dictionary.Set(KnownName.Height, PdfValue.FromInteger(height));
        dictionary.Set(KnownName.BitsPerComponent, PdfValue.FromInteger(bits));
        if (!colorSpace.IsNull)
        {
            dictionary.Set(KnownName.ColorSpace, colorSpace);
        }

        var compressed = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(samples, ref compressed);
            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            return new(dictionary, compressed.ToArray());
        }
        finally
        {
            compressed.Dispose();
        }
    }
}
