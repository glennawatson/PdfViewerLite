// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// What a document keeps between renders: loaded fonts, decoded images, colour spaces, shadings, recorded pattern cells
/// and soft masks. Every cache is safe to use from many threads.
/// </summary>
[DebuggerDisplay("PdfRenderCache: {Images.Count} images, {Images.Bytes} bytes")]
internal sealed class PdfRenderCache
{
    /// <summary>Initializes a new instance of the <see cref="PdfRenderCache"/> class.</summary>
    /// <param name="document">The document.</param>
    internal PdfRenderCache(PdfDocument document)
        : this(document, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfRenderCache"/> class whose content converts device colours through the output intent.</summary>
    /// <param name="document">The document.</param>
    /// <param name="deviceColors">The output intent substitutes for the device colour spaces, or <see langword="null"/> for none.</param>
    internal PdfRenderCache(PdfDocument document, OutputIntentColors? deviceColors)
    {
        Document = document;
        DeviceColors = deviceColors;
        LowerCa = document.Objects.Names.Intern("ca");
        GraphicsStateName = document.Objects.Names.Intern("GS");
        Thumb = document.Objects.Names.Intern("Thumb");
    }

    /// <summary>Gets the output intent substitutes for the device colour spaces, or <see langword="null"/> when content uses the fixed conversions.</summary>
    internal OutputIntentColors? DeviceColors { get; }

    /// <summary>Gets the <c>/GS</c> name generated appearances give their graphics state.</summary>
    internal PdfName GraphicsStateName { get; }

    /// <summary>Gets the <c>/Thumb</c> name of a page's thumbnail image.</summary>
    internal PdfName Thumb { get; }

    /// <summary>Gets or sets a value indicating whether pages recorded from now on simulate image overprint.</summary>
    internal bool SimulateOverprint
    {
        get => Volatile.Read(ref field);
        set => Volatile.Write(ref field, value);
    }

    /// <summary>Gets or sets the tint pages recorded from now on draw over fillable form fields.</summary>
    internal PdfFormHighlight FormHighlight
    {
        get => PdfFormHighlight.Unpack(FormHighlightBits);
        set => FormHighlightBits = value.Pack();
    }

    /// <summary>Gets the document.</summary>
    internal PdfDocument Document { get; }

    /// <summary>Gets the document's name table.</summary>
    internal PdfNameTable Names => Document.Objects.Names;

    /// <summary>Gets the <c>/ca</c> name, which the known-name list does not hold.</summary>
    internal PdfName LowerCa { get; }

    /// <summary>Gets the loaded fonts.</summary>
    internal PdfFontCache Fonts { get; } = new();

    /// <summary>Gets the decoded images, bounded by their pixel bytes.</summary>
    internal ImageCache Images { get; } = new();

    /// <summary>Gets the parsed colour space arrays.</summary>
    internal ObjectCache<PdfArray, PdfColorSpace> ColorSpaces { get; } = new();

    /// <summary>Gets the parsed shadings by dictionary.</summary>
    internal ObjectCache<PdfDictionary, PdfShading> Shadings { get; } = new();

    /// <summary>Gets the recorded tiling cells.</summary>
    internal ObjectCache<PatternKey, PatternCell> Cells { get; } = new();

    /// <summary>Gets the recorded soft masks.</summary>
    internal ObjectCache<SoftMaskKey, PdfSoftMask> SoftMasks { get; } = new();

    /// <summary>Gets or sets the form field tint, packed by <see cref="PdfFormHighlight.Pack"/>, so one atomic write publishes it.</summary>
    private long FormHighlightBits
    {
        get => Volatile.Read(ref field);
        set => Volatile.Write(ref field, value);
    }

    /// <summary>Converts a decoded image to a Skia image.</summary>
    /// <param name="data">The decoded image.</param>
    /// <returns>The Skia image, which the caller owns.</returns>
    internal static SKImage? ToSkImage(PdfImageData data)
    {
        if (data.Width <= 0 || data.Height <= 0 || data.Pixels.Length == 0)
        {
            return null;
        }

        var info = GetInfo(data);
        var rowBytes = data.Width * info.BytesPerPixel;
        if (!data.IsPinned)
        {
            return SKImage.FromPixelCopy(info, data.Pixels, rowBytes);
        }

        // The array sits on the pinned object heap and the release context keeps it alive, so Skia owns the decoder's
        // output without copying it. The array is freed once Skia drops its last reference to the image.
        using var pixmap = new SKPixmap(info, Marshal.UnsafeAddrOfPinnedArrayElement(data.Pixels, 0), rowBytes);
        return SKImage.FromPixels(pixmap, ReleasePixels, data.Pixels);
    }

    /// <summary>
    /// Empties every cache when the document is disposed. Images are released as their last user finishes, and later
    /// image requests fail with <see cref="ObjectDisposedException"/>; the other caches are emptied, so renders still
    /// running either finish with what they hold or fail when they read from the closed document.
    /// </summary>
    internal void Close()
    {
        Images.Close();
        Fonts.Clear();
        ColorSpaces.Clear();
        Shadings.Clear();
        Cells.Clear();
        SoftMasks.Clear();
    }

    /// <summary>Drops font-dependent entries after newly requested font data becomes available.</summary>
    internal void InvalidateFonts()
    {
        Fonts.Clear();
        Cells.Clear();
        SoftMasks.Clear();
    }

    /// <summary>Gets an image XObject ready to draw and marks it in use.</summary>
    /// <param name="stream">The image stream.</param>
    /// <returns>The image, which the caller releases; <see langword="null"/> when it cannot be decoded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ImageEntry? AcquireImage(PdfStream stream) =>
        Images.Acquire(stream, DeviceColors, Decode);

    /// <summary>Gets a colour space from a value.</summary>
    /// <param name="value">A name or colour space array, resolved.</param>
    /// <param name="resources">The /ColorSpace resource dictionary, or null.</param>
    /// <returns>The colour space.</returns>
    internal PdfColorSpace GetColorSpace(PdfValue value, PdfDictionary? resources)
    {
        using var scope = OutputIntentColors.Enter(DeviceColors);
        return value.AsArray() is not { } array
            ? PdfColorSpace.Parse(value, resources)
            : ColorSpaces.GetOrCreate(array, resources, static (key, state) => PdfColorSpace.Parse(PdfValue.FromArray(key), state))
                ?? PdfColorSpace.DeviceGray;
    }

    /// <summary>Describes the pixels of a decoded image: coverage, gray or BGRA.</summary>
    /// <param name="data">The decoded image.</param>
    /// <returns>The Skia description.</returns>
    private static SKImageInfo GetInfo(PdfImageData data) => data switch
    {
        { IsStencilMask: true } => new(data.Width, data.Height, SKColorType.Alpha8, SKAlphaType.Premul),
        { IsGray: true } => new(data.Width, data.Height, SKColorType.Gray8, SKAlphaType.Opaque),
        _ => new(data.Width, data.Height, SKColorType.Bgra8888, SKAlphaType.Premul),
    };

    /// <summary>Called when Skia lets go of pixels it was given.</summary>
    /// <param name="address">The pixel address.</param>
    /// <param name="context">The pixel array.</param>
    private static void ReleasePixels(nint address, object context)
    {
        // Nothing to free: Skia held the only reference to the context, which is the pinned array, and drops it now.
    }

    /// <summary>Decodes an image stream.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="colors">The output intent substitutes for device colour spaces, or <see langword="null"/>.</param>
    /// <returns>The entry, or null.</returns>
    private static ImageEntry? Decode(PdfStream stream, OutputIntentColors? colors)
    {
        using var scope = OutputIntentColors.Enter(colors);
        var data = PdfImageDecoder.DecodeCompact(stream);
        if (data is null)
        {
            return null;
        }

        if (data.UnsupportedCodec != PdfImageCodec.None)
        {
            var image = PdfRenderHooks.UnsupportedImageDecoder?.Invoke(stream);
            return image is null ? null : new ImageEntry(image, false, true);
        }

        var converted = ToSkImage(data);
        return converted is null ? null : new ImageEntry(converted, data.IsStencilMask, data.Interpolate);
    }
}
