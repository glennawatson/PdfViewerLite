// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>Image signatures stored as stamp appearances with a PDF soft mask.</summary>
internal static unsafe partial class PdfiumAnnotations
{
    /// <summary>PDFium's straight-alpha BGRA bitmap format.</summary>
    private const int ImageBgraFormat = 4;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int ImageChannels = 4;

    /// <summary>Copies an image into a signature annotation. The caller holds the PDFium lock.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The bounds in viewer page coordinates.</param>
    /// <param name="pixels">The straight-alpha BGRA image.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <returns>The annotation index, or -1.</returns>
    internal static int AddImageSignature(PdfiumDocumentHandle document, PdfiumPage page, PageRect bounds, ReadOnlySpan<byte> pixels, int width, int height)
    {
        var image = CreateImage(document, pixels, width, height);
        return image == 0 ? -1 : AppendImage(page, bounds, image);
    }

    /// <summary>Creates an image object that owns a copy of the pixels.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pixels">The source pixels.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The owned image object, or zero.</returns>
    private static nint CreateImage(PdfiumDocumentHandle document, ReadOnlySpan<byte> pixels, int width, int height)
    {
        var image = NativeMethods.FPDFPageObj_NewImageObj(document);
        if (image == 0)
        {
            return 0;
        }

        fixed (byte* source = pixels)
        {
            var bitmap = NativeMethods.FPDFBitmap_CreateEx(width, height, ImageBgraFormat, source, width * ImageChannels);
            if (bitmap != 0)
            {
                try
                {
                    if (NativeMethods.FPDFImageObj_SetBitmap(null, 0, image, bitmap) != 0)
                    {
                        return image;
                    }
                }
                finally
                {
                    NativeMethods.FPDFBitmap_Destroy(bitmap);
                }
            }
        }

        NativeMethods.FPDFPageObj_Destroy(image);
        return 0;
    }

    /// <summary>Positions the image and transfers ownership to a removable signature.</summary>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The bounds in viewer page coordinates.</param>
    /// <param name="image">The image owned by this method.</param>
    /// <returns>The annotation index, or -1.</returns>
    private static int AppendImage(PdfiumPage page, PageRect bounds, nint image)
    {
        var annotation = NativeMethods.FPDFPage_CreateAnnot(page.Handle, SubtypeStamp);
        if (annotation == 0)
        {
            NativeMethods.FPDFPageObj_Destroy(image);
            return -1;
        }

        var index = NativeMethods.FPDFPage_GetAnnotIndex(page.Handle, annotation);
        var appended = false;
        try
        {
            PositionImage(page, bounds, image);
            PdfBounds rectangle = default;
            rectangle.Add(page, new(bounds.Left, bounds.Top));
            rectangle.Add(page, new(bounds.Right, bounds.Bottom));
            Finish(annotation, rectangle.ToRect(0), AnnotationColors.Ink, string.Empty, SignatureSubject);
            appended = NativeMethods.FPDFAnnot_AppendObject(annotation, image) != 0;
            return appended ? index : -1;
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
            if (!appended)
            {
                NativeMethods.FPDFPageObj_Destroy(image);
                _ = NativeMethods.FPDFPage_RemoveAnnot(page.Handle, index);
            }
        }
    }

    /// <summary>Maps an image's unit square to the page, including stored page rotation.</summary>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The image bounds.</param>
    /// <param name="image">The image object.</param>
    private static void PositionImage(PdfiumPage page, PageRect bounds, nint image)
    {
        page.ToPdf(new(bounds.Left, bounds.Bottom), out var left, out var bottom);
        page.ToPdf(new(bounds.Right, bounds.Bottom), out var right, out var rightY);
        page.ToPdf(new(bounds.Left, bounds.Top), out var topX, out var top);
        NativeMethods.FPDFPageObj_Transform(image, right - left, rightY - bottom, topX - left, top - bottom, left, bottom);
    }
}
