// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>Reads what an image's colour space allows the optimiser to do.</summary>
internal static class ImageColor
{
    /// <summary>The components of an RGB space.</summary>
    private const int RgbComponents = 3;

    /// <summary>The index of an ICC profile stream in an /ICCBased array.</summary>
    private const int ProfileIndex = 1;

    /// <summary>
    /// Gets how many components a colour space has, when JPEG can hold its samples: one for grey and three for RGB
    /// spaces. CMYK, Lab, indexed, separation and DeviceN images are kept lossless.
    /// </summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <returns>1, 3, or 0 when the space cannot be re-encoded lossily.</returns>
    internal static int LossyComponents(PdfDictionary dictionary)
    {
        var space = dictionary.Get(KnownName.ColorSpace);
        if (space.Kind == PdfKind.Name)
        {
            return space.AsName().ToKnownName() switch
            {
                KnownName.DeviceGray or KnownName.G => 1,
                KnownName.DeviceRGB or KnownName.RGB => RgbComponents,
                _ => 0,
            };
        }

        return space.AsArray() is { Count: > 0 } array ? ArrayComponents(array) : 0;
    }

    /// <summary>Determines whether each sample has one component, so 1-bit samples can be fax coded bit for bit.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <returns><see langword="true"/> for stencil masks and grey, indexed, separation and one-component ICC images.</returns>
    internal static bool HasOneComponent(PdfDictionary dictionary)
    {
        if (dictionary.GetBoolean(KnownName.ImageMask))
        {
            return true;
        }

        var space = dictionary.Get(KnownName.ColorSpace);
        if (space.Kind == PdfKind.Name)
        {
            return space.AsName().ToKnownName() is KnownName.DeviceGray or KnownName.G or KnownName.CalGray;
        }

        if (space.AsArray() is not { Count: > 0 } array)
        {
            return false;
        }

        return array.GetName(0).ToKnownName() switch
        {
            KnownName.CalGray or KnownName.Indexed or KnownName.I or KnownName.Separation => true,
            KnownName.ICCBased => IccComponents(array) == 1,
            _ => false,
        };
    }

    /// <summary>Determines whether an image is colour-key masked, which needs its exact sample values.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <returns><see langword="true"/> when /Mask is an array of ranges.</returns>
    internal static bool HasColorKey(PdfDictionary dictionary) => dictionary.Get(KnownName.Mask).Kind == PdfKind.Array;

    /// <summary>Determines whether an image's soft mask has a /Matte, which requires the image and mask to keep the same size.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <param name="matte">The <c>/Matte</c> name.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    internal static bool HasMatte(PdfDictionary dictionary, PdfName matte) =>
        dictionary.Get(KnownName.SMask).AsStream()?.Dictionary.ContainsKey(matte) == true;

    /// <summary>Gets the components of a colour space array JPEG can hold.</summary>
    /// <param name="array">The colour space array.</param>
    /// <returns>1, 3, or 0.</returns>
    private static int ArrayComponents(PdfArray array) => array.GetName(0).ToKnownName() switch
    {
        KnownName.CalGray => 1,
        KnownName.CalRGB => RgbComponents,
        KnownName.ICCBased => IccComponents(array) is var n && n is 1 or RgbComponents ? n : 0,
        _ => 0,
    };

    /// <summary>Gets an ICC profile's component count.</summary>
    /// <param name="array">The /ICCBased array.</param>
    /// <returns>The /N value, or zero.</returns>
    private static int IccComponents(PdfArray array) => array.Get(ProfileIndex).AsStream()?.Dictionary.GetInt32(KnownName.N, 0) ?? 0;
}
