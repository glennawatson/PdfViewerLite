// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.HyperPdf;

/// <content>Picture stamps and image signatures: stamps whose appearance draws an image with its soft mask.</content>
internal sealed partial class HyperPdfAnnotations
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>Gets the name of the image an appearance draws.</summary>
    private static ReadOnlySpan<byte> ImageResource => "Im0"u8;

    /// <inheritdoc/>
    public int AddImageStamp(int pageIndex, PageRect bounds, ReadOnlySpan<byte> pixels, int width, int height)
    {
        if (width <= 0 || height <= 0 || pixels.Length < (long)width * height * BgraBytes)
        {
            return -1;
        }

        lock (_gate)
        {
            return GetPage(pageIndex) is { } page ? AddImage(pageIndex, page, bounds, pixels, new(width, height), StampSubject) : -1;
        }
    }

    /// <inheritdoc/>
    public int AddImageSignature(int pageIndex, PageRect bounds, ReadOnlySpan<byte> pixels, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bounds.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bounds.Height);
        if (pixels.Length != checked(width * height * BgraBytes))
        {
            throw new ArgumentException("The image dimensions must match its BGRA pixels.", nameof(pixels));
        }

        lock (_gate)
        {
            return GetPage(pageIndex) is { } page ? AddImage(pageIndex, page, bounds, pixels, new(width, height), SignatureSubject) : -1;
        }
    }

    /// <summary>Adds the image and a stamp whose appearance maps the image's unit square onto the bounds, page rotation included.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The bounds in page space.</param>
    /// <param name="pixels">The straight-alpha BGRA pixels.</param>
    /// <param name="size">The image size in pixels.</param>
    /// <param name="subject">What the stamp is: a signature or a picture stamp.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    private int AddImage(int pageIndex, PdfPage page, PageRect bounds, ReadOnlySpan<byte> pixels, PixelSize size, ReadOnlySpan<byte> subject)
    {
        var image = PdfImages.AddBgraImage(_store, pixels[..(size.Width * size.Height * BgraBytes)], size.Width, size.Height);
        var bottomLeft = ToUser(page, new(bounds.Left, bounds.Bottom));
        var bottomRight = ToUser(page, new(bounds.Right, bounds.Bottom));
        var topLeft = ToUser(page, new(bounds.Left, bounds.Top));
        var rectangle = ToUserRectangle(page, bounds);
        var stamp = PdfAnnotations.Create(_store, KnownName.Stamp, rectangle);
        var images = new PdfDictionary(_store, 1);
        images.Set(_store.Names.Intern(ImageResource), PdfValue.FromReference(image));
        var resources = new PdfDictionary(_store, 1);
        resources.Set(KnownName.XObject, PdfValue.FromDictionary(images));
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            var across = bottomRight - bottomLeft;
            var up = topLeft - bottomLeft;
            builder.Transform(across.X, across.Y, up.X, up.Y, bottomLeft.X, bottomLeft.Y);
            builder.DrawXObject(ImageResource);
            builder.RestoreState();
            _ = PdfAnnotations.SetNormalAppearance(_store, stamp, builder.ToFormXObject(_store, rectangle, resources));
        }
        finally
        {
            builder.Dispose();
        }

        // A picture keeps its own colours, so it records none; readers show the kind's default colour, as with PDFium.
        return Add(pageIndex, page, stamp, null, string.Empty, subject);
    }
}
