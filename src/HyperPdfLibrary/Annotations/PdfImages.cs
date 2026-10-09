// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <summary>Writes pictures into a document as image XObjects, keeping their transparency in a soft mask.</summary>
public static class PdfImages
{
    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The bytes of an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bits of each colour component.</summary>
    private const int BitsPerComponent = 8;

    /// <summary>The offset of red in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of alpha in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The entries of an image dictionary.</summary>
    private const int ImageEntries = 8;

    /// <summary>The offset of blue in an RGB pixel.</summary>
    private const int BlueOffset = 2;

    /// <summary>
    /// Adds an image from straight-alpha BGRA pixels, rows top to bottom. The colour is Flate-compressed RGB; any
    /// transparency is kept in a Flate-compressed gray soft mask.
    /// </summary>
    /// <param name="store">The document.</param>
    /// <param name="pixels">The pixels.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <returns>The image's object id.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The size is not positive.</exception>
    /// <exception cref="ArgumentException">The pixels do not fill the size.</exception>
    public static PdfObjectId AddBgraImage(PdfObjectStore store, ReadOnlySpan<byte> pixels, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var count = checked(width * height);
        if (pixels.Length < checked(count * BgraBytes))
        {
            throw new ArgumentException("The pixels do not fill the image.", nameof(pixels));
        }

        var rgb = ArrayPool<byte>.Shared.Rent(count * RgbBytes);
        var alpha = ArrayPool<byte>.Shared.Rent(count);
        try
        {
            var opaque = Split(pixels[..(count * BgraBytes)], rgb, alpha);
            var image = CreateImage(store, width, height, KnownName.DeviceRGB, rgb.AsSpan(0, count * RgbBytes));
            if (!opaque)
            {
                var mask = CreateImage(store, width, height, KnownName.DeviceGray, alpha.AsSpan(0, count));
                image.Dictionary.Set(KnownName.SMask, PdfValue.FromReference(store.Add(PdfValue.FromStream(mask))));
            }

            return store.Add(PdfValue.FromStream(image));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rgb);
            ArrayPool<byte>.Shared.Return(alpha);
        }
    }

    /// <summary>Splits BGRA pixels into RGB and alpha planes.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="rgb">Receives the colour.</param>
    /// <param name="alpha">Receives the alpha.</param>
    /// <returns><see langword="true"/> when every pixel is opaque.</returns>
    private static bool Split(ReadOnlySpan<byte> pixels, Span<byte> rgb, Span<byte> alpha)
    {
        var opaque = true;
        var count = pixels.Length / BgraBytes;
        for (var i = 0; i < count; i++)
        {
            var pixel = pixels.Slice(i * BgraBytes, BgraBytes);
            var target = rgb.Slice(i * RgbBytes, RgbBytes);
            target[0] = pixel[RedOffset];
            target[1] = pixel[1];
            target[BlueOffset] = pixel[0];
            alpha[i] = pixel[AlphaOffset];
            opaque &= pixel[AlphaOffset] == byte.MaxValue;
        }

        return opaque;
    }

    /// <summary>Creates a Flate-compressed image stream.</summary>
    /// <param name="store">The document.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="colorSpace">The colour space.</param>
    /// <param name="samples">The samples.</param>
    /// <returns>The stream.</returns>
    private static PdfStream CreateImage(PdfObjectStore store, int width, int height, KnownName colorSpace, ReadOnlySpan<byte> samples)
    {
        var dictionary = new PdfDictionary(store, ImageEntries);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.XObject));
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Image));
        dictionary.Set(KnownName.Width, PdfValue.FromInteger(width));
        dictionary.Set(KnownName.Height, PdfValue.FromInteger(height));
        dictionary.Set(KnownName.ColorSpace, PdfValue.FromName(colorSpace));
        dictionary.Set(KnownName.BitsPerComponent, PdfValue.FromInteger(BitsPerComponent));
        dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
        var compressed = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(samples, ref compressed);
            return new(dictionary, compressed.ToArray());
        }
        finally
        {
            compressed.Dispose();
        }
    }
}
