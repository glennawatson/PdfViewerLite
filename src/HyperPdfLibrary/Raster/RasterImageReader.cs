// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Raster;

/// <summary>
/// Describes an image from its dictionary and the matrix it is painted with, without decoding it. Names are compared as
/// UTF-8 bytes; strings are made only for the public report, from shared constants for the names PDF defines.
/// </summary>
internal static class RasterImageReader
{
    /// <summary>The number of points in an inch.</summary>
    private const float PointsPerInch = 72F;

    /// <summary>The Flate filter's full name.</summary>
    private const string Flate = "FlateDecode";

    /// <summary>The CCITT fax filter's full name.</summary>
    private const string Ccitt = "CCITTFaxDecode";

    /// <summary>The JPEG filter's full name.</summary>
    private const string Dct = "DCTDecode";

    /// <summary>The JBIG2 filter's full name.</summary>
    private const string Jbig2 = "JBIG2Decode";

    /// <summary>The JPEG 2000 filter's full name.</summary>
    private const string Jpx = "JPXDecode";

    /// <summary>The grey device colour space.</summary>
    private const string Gray = "DeviceGray";

    /// <summary>The RGB device colour space.</summary>
    private const string Rgb = "DeviceRGB";

    /// <summary>The CMYK device colour space.</summary>
    private const string Cmyk = "DeviceCMYK";

    /// <summary>The indexed colour space.</summary>
    private const string IndexedSpace = "Indexed";

    /// <summary>The colour space name reported for a stencil mask.</summary>
    private const string MaskSpace = "ImageMask";

    /// <summary>Describes an image.</summary>
    /// <param name="names">The document's name table.</param>
    /// <param name="dictionary">The image or inline image dictionary; inline abbreviations are understood when <paramref name="inline"/> is set.</param>
    /// <param name="ctm">The matrix that maps the image's unit square to the page.</param>
    /// <param name="inline">Whether the dictionary belongs to an inline image.</param>
    /// <param name="colorSpaces">The /ColorSpace resource dictionary in force, or null.</param>
    /// <returns>The description.</returns>
    internal static PdfRasterImage Describe(PdfNameTable names, PdfDictionary dictionary, Matrix3x2 ctm, bool inline, PdfDictionary? colorSpaces)
    {
        var width = dictionary.GetInt32(inline ? KnownName.W : KnownName.Width, dictionary.GetInt32(KnownName.Width));
        var height = dictionary.GetInt32(inline ? KnownName.H : KnownName.Height, dictionary.GetInt32(KnownName.Height));
        var isMask = dictionary.GetBoolean(inline ? KnownName.IM : KnownName.ImageMask, dictionary.GetBoolean(KnownName.ImageMask));
        var bits = isMask ? 1 : dictionary.GetInt32(inline ? KnownName.BPC : KnownName.BitsPerComponent, dictionary.GetInt32(KnownName.BitsPerComponent));
        var filters = ReadFilters(names, FilterValue(dictionary, inline), out var allowed);
        var space = isMask ? MaskSpace : ReadColorSpace(names, dictionary, inline, colorSpaces);
        var widthPoints = MathF.Sqrt((ctm.M11 * ctm.M11) + (ctm.M12 * ctm.M12));
        var heightPoints = MathF.Sqrt((ctm.M21 * ctm.M21) + (ctm.M22 * ctm.M22));
        return new(width, height, filters, space, bits, ToDpi(width, widthPoints), ToDpi(height, heightPoints), inline, allowed);
    }

    /// <summary>Gets a filter's full name.</summary>
    /// <param name="name">The name's UTF-8 bytes; inline abbreviations are expanded.</param>
    /// <returns>The full name.</returns>
    internal static string Expand(ReadOnlySpan<byte> name) => name switch
    {
        _ when name.SequenceEqual("Fl"u8) || name.SequenceEqual("FlateDecode"u8) => Flate,
        _ when name.SequenceEqual("CCF"u8) || name.SequenceEqual("CCITTFaxDecode"u8) => Ccitt,
        _ when name.SequenceEqual("DCT"u8) || name.SequenceEqual("DCTDecode"u8) => Dct,
        _ when name.SequenceEqual("JBIG2Decode"u8) => Jbig2,
        _ when name.SequenceEqual("JPXDecode"u8) => Jpx,
        _ when name.SequenceEqual("AHx"u8) => "ASCIIHexDecode",
        _ when name.SequenceEqual("A85"u8) => "ASCII85Decode",
        _ when name.SequenceEqual("LZW"u8) => "LZWDecode",
        _ when name.SequenceEqual("RL"u8) => "RunLengthDecode",
        _ => Encoding.UTF8.GetString(name),
    };

    /// <summary>Determines whether a filter is one PDF/R allows on image data.</summary>
    /// <param name="name">The name's UTF-8 bytes, abbreviated or in full.</param>
    /// <returns><see langword="true"/> for Flate, CCITT, DCT, JBIG2 and JPX.</returns>
    internal static bool IsAllowed(ReadOnlySpan<byte> name) => Expand(name) is Flate or Ccitt or Dct or Jbig2 or Jpx;

    /// <summary>Converts a sample count and a painted length to samples per inch.</summary>
    /// <param name="samples">The samples along the edge.</param>
    /// <param name="points">The painted length in points.</param>
    /// <returns>The resolution; 0 when the length is 0.</returns>
    private static float ToDpi(int samples, float points) => points > 0 ? samples * PointsPerInch / points : 0;

    /// <summary>Gets the /Filter entry; an inline image may abbreviate it to <c>/F</c>.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <param name="inline">Whether it is an inline image.</param>
    /// <returns>The value; null when missing.</returns>
    private static PdfValue FilterValue(PdfDictionary dictionary, bool inline)
    {
        var value = dictionary.Get(KnownName.Filter);
        return value.IsNull && inline ? dictionary.Get(KnownName.F) : value;
    }

    /// <summary>Reads a /Filter value as full names.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="value">A name, an array of names or null.</param>
    /// <param name="allowed">Receives whether every filter is allowed.</param>
    /// <returns>The names in order.</returns>
    private static string[] ReadFilters(PdfNameTable names, PdfValue value, out bool allowed)
    {
        allowed = true;
        if (value.AsName() is { IsNone: false } single)
        {
            allowed = IsAllowed(names.GetSpelling(single));
            return [Expand(names.GetSpelling(single))];
        }

        if (value.AsArray() is not { Count: > 0 } array)
        {
            return [];
        }

        var result = new string[array.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var spelling = names.GetSpelling(array.GetName(i));
            allowed &= IsAllowed(spelling);
            result[i] = Expand(spelling);
        }

        return result;
    }

    /// <summary>Reads the colour space family.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="dictionary">The image dictionary.</param>
    /// <param name="inline">Whether it is an inline image.</param>
    /// <param name="colorSpaces">The /ColorSpace resources, or null.</param>
    /// <returns>The family name, or an empty string.</returns>
    private static string ReadColorSpace(PdfNameTable names, PdfDictionary dictionary, bool inline, PdfDictionary? colorSpaces)
    {
        var value = ColorSpaceValue(dictionary, inline);
        if (value.AsArray() is { Count: > 0 } array)
        {
            return Family(names.GetSpelling(array.GetName(0)));
        }

        if (value.AsName() is not { IsNone: false } name)
        {
            return string.Empty;
        }

        var spelling = names.GetSpelling(name);
        var resource = colorSpaces?.Get(name) ?? default;
        return resource.IsNull ? Family(spelling) : ResolveResource(names, resource, spelling);
    }

    /// <summary>Gets the colour space entry, under <c>/CS</c> for an inline image that uses the abbreviation.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <param name="inline">Whether it is an inline image.</param>
    /// <returns>The value; null when missing.</returns>
    private static PdfValue ColorSpaceValue(PdfDictionary dictionary, bool inline)
    {
        var value = dictionary.Get(KnownName.ColorSpace);
        return value.IsNull && inline ? dictionary.Get(KnownName.CS) : value;
    }

    /// <summary>Reports what a named colour space resource is made of.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="resource">The resource value.</param>
    /// <param name="fallback">The name's UTF-8 bytes, used when the resource is missing.</param>
    /// <returns>The family name.</returns>
    private static string ResolveResource(PdfNameTable names, PdfValue resource, ReadOnlySpan<byte> fallback)
    {
        if (resource.AsArray() is { Count: > 0 } named)
        {
            return Family(names.GetSpelling(named.GetName(0)));
        }

        return resource.AsName() is { IsNone: false } alias ? Family(names.GetSpelling(alias)) : Family(fallback);
    }

    /// <summary>Gets a colour space family's full name, expanding inline abbreviations.</summary>
    /// <param name="name">The name's UTF-8 bytes.</param>
    /// <returns>The full name.</returns>
    private static string Family(ReadOnlySpan<byte> name) => name switch
    {
        _ when name.SequenceEqual("G"u8) || name.SequenceEqual("DeviceGray"u8) => Gray,
        _ when name.SequenceEqual("RGB"u8) || name.SequenceEqual("DeviceRGB"u8) => Rgb,
        _ when name.SequenceEqual("CMYK"u8) || name.SequenceEqual("DeviceCMYK"u8) => Cmyk,
        _ when name.SequenceEqual("I"u8) || name.SequenceEqual("Indexed"u8) => IndexedSpace,
        _ when name.SequenceEqual("ICCBased"u8) => "ICCBased",
        _ => Encoding.UTF8.GetString(name),
    };
}
