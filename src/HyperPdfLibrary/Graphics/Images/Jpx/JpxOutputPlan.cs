// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Decides how decoded channels become PDF samples, following PDFium: its sYCC and gray guesses, then
/// <c>JpxDecodeConversion</c>. The image dictionary's /ColorSpace, when present, wins over the file's colour box but
/// must agree with it for the device spaces; without one, the colour box or the channel count decides.
/// </summary>
/// <param name="Action">The conversion.</param>
/// <param name="ColorSpace">The colour space the samples are read in.</param>
/// <param name="Components">The samples per pixel written.</param>
/// <param name="Sycc">The sYCC conversion to apply, or none.</param>
/// <param name="KeepsDictionarySpace">Whether <see cref="ColorSpace"/> is the image dictionary's own.</param>
[DebuggerDisplay("JpxOutputPlan: {Action}, {Components} components")]
internal sealed record JpxOutputPlan(JpxDecodeAction Action, PdfColorSpace ColorSpace, int Components, JpxSyccLayout Sycc, bool KeepsDictionarySpace)
{
    /// <summary>The channels of an RGB or sYCC image.</summary>
    private const int ColourChannels = 3;

    /// <summary>The channels of a CMYK or alpha-carrying RGB image.</summary>
    private const int FourChannels = 4;

    /// <summary>The most channels PDFium treats as gray: gray, or gray with alpha.</summary>
    private const int GrayChannels = 2;

    /// <summary>The second chroma channel.</summary>
    private const int SecondChroma = 2;

    /// <summary>The chroma subsampling factor of 4:2:2 and 4:2:0.</summary>
    private const int Halved = 2;

    /// <summary>Plans the conversion.</summary>
    /// <param name="declared">The colour space the file declares.</param>
    /// <param name="channels">The channels.</param>
    /// <param name="image">The decoded planes.</param>
    /// <param name="dictionarySpace">The image dictionary's /ColorSpace, or <see langword="null"/>.</param>
    /// <returns>The plan, or <see langword="null"/> when PDFium would refuse the image.</returns>
    internal static JpxOutputPlan? Create(JpxColorSpace declared, ReadOnlySpan<JpxChannel> channels, JpxDecodedImage image, PdfColorSpace? dictionarySpace)
    {
        var count = channels.Length;
        var (space, sycc) = ResolveSycc(GuessSpace(declared, channels, image), channels, image);
        var action = dictionarySpace is null ? FromImage(space, count) : FromBoth(space, count, dictionarySpace);
        if (action is not { } chosen)
        {
            return null;
        }

        var colorSpace = chosen switch
        {
            JpxDecodeAction.UseGray => PdfColorSpace.DeviceGray,
            JpxDecodeAction.UseRgb or JpxDecodeAction.ConvertArgbToRgb => PdfColorSpace.DeviceRgb,
            JpxDecodeAction.UseCmyk => PdfColorSpace.DeviceCmyk,
            _ => dictionarySpace ?? PdfColorSpace.FromComponents(ComponentsOf(space, count)),
        };

        var components = colorSpace.Components;
        return components > count ? null : new(chosen, colorSpace, components, sycc, ReferenceEquals(colorSpace, dictionarySpace));
    }

    /// <summary>Decides whether an sYCC image converts to RGB, as PDFium's <c>color_sycc_to_rgb</c> does.</summary>
    /// <param name="space">The guessed colour space.</param>
    /// <param name="channels">The channels.</param>
    /// <param name="image">The decoded planes.</param>
    /// <returns>The colour space after any conversion, and the conversion.</returns>
    private static SpaceChoice ResolveSycc(JpxColorSpace space, ReadOnlySpan<JpxChannel> channels, JpxDecodedImage image)
    {
        if (space != JpxColorSpace.Sycc)
        {
            return new(space, JpxSyccLayout.None);
        }

        if (channels.Length < ColourChannels)
        {
            return new(JpxColorSpace.Gray, JpxSyccLayout.None);
        }

        var layout = SyccLayout(channels, image);
        return new(layout == JpxSyccLayout.None ? space : JpxColorSpace.Srgb, layout);
    }

    /// <summary>Applies PDFium's guesses: three channels with subsampled chroma are sYCC, and one or two channels are gray.</summary>
    /// <param name="declared">The declared colour space.</param>
    /// <param name="channels">The channels.</param>
    /// <param name="image">The decoded planes.</param>
    /// <returns>The colour space PDFium sees.</returns>
    private static JpxColorSpace GuessSpace(JpxColorSpace declared, ReadOnlySpan<JpxChannel> channels, JpxDecodedImage image)
    {
        var components = image.Geometry.Components;
        if (declared != JpxColorSpace.Sycc && channels.Length == ColourChannels
            && components[channels[0].Component].Dx == components[channels[0].Component].Dy
            && components[channels[1].Component].Dx != 1)
        {
            return JpxColorSpace.Sycc;
        }

        return channels.Length <= GrayChannels ? JpxColorSpace.Gray : declared;
    }

    /// <summary>Finds which sYCC sampling the channels have, with the plane sizes PDFium checks.</summary>
    /// <param name="channels">The channels.</param>
    /// <param name="image">The decoded planes.</param>
    /// <returns>The layout, or none when PDFium would leave the samples unconverted.</returns>
    private static JpxSyccLayout SyccLayout(ReadOnlySpan<JpxChannel> channels, JpxDecodedImage image)
    {
        var components = image.Geometry.Components;
        var cb = components[channels[1].Component];
        var cr = components[channels[SecondChroma].Component];
        if (!IsFullResolution(components[channels[0].Component]))
        {
            return JpxSyccLayout.None;
        }

        if (IsFullResolution(cb) && IsFullResolution(cr))
        {
            return JpxSyccLayout.Full;
        }

        var areas = image.Areas;
        return HalvedLayout(cb, cr, areas[channels[0].Component], areas[channels[1].Component], areas[channels[SecondChroma].Component]);
    }

    /// <summary>Determines whether a component is sampled at every reference grid point.</summary>
    /// <param name="component">The component.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsFullResolution(in JpxComponentInfo component) => component is { Dx: 1, Dy: 1 };

    /// <summary>Finds the 4:2:2 or 4:2:0 layout of halved chroma, with the plane sizes PDFium checks.</summary>
    /// <param name="cb">The Cb component.</param>
    /// <param name="cr">The Cr component.</param>
    /// <param name="luma">The luma plane.</param>
    /// <param name="blue">The Cb plane.</param>
    /// <param name="red">The Cr plane.</param>
    /// <returns>The layout, or none.</returns>
    private static JpxSyccLayout HalvedLayout(in JpxComponentInfo cb, in JpxComponentInfo cr, in JpxRectangle luma, in JpxRectangle blue, in JpxRectangle red)
    {
        if (cb.Dx != Halved || cr.Dx != Halved || !ChromaMatches(luma, blue, red))
        {
            return JpxSyccLayout.None;
        }

        if (cb.Dy == 1 && cr.Dy == 1 && luma.Height == blue.Height)
        {
            return JpxSyccLayout.HalfWidth;
        }

        return cb.Dy == Halved && cr.Dy == Halved && JpxRectangle.CeilDivide(luma.Height, Halved) == blue.Height ? JpxSyccLayout.Quarter : JpxSyccLayout.None;
    }

    /// <summary>Checks that two chroma planes match and are half the luma width, rounded up.</summary>
    /// <param name="luma">The luma plane.</param>
    /// <param name="blue">The Cb plane.</param>
    /// <param name="red">The Cr plane.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    private static bool ChromaMatches(in JpxRectangle luma, in JpxRectangle blue, in JpxRectangle red) =>
        JpxRectangle.CeilDivide(luma.Width, Halved) == blue.Width && blue.Width == red.Width && blue.Height == red.Height;

    /// <summary>Chooses the conversion from the file alone, when the dictionary has no /ColorSpace.</summary>
    /// <param name="space">The file's colour space.</param>
    /// <param name="count">The channels.</param>
    /// <returns>The conversion.</returns>
    private static JpxDecodeAction? FromImage(JpxColorSpace space, int count) => space switch
    {
        JpxColorSpace.Unknown or JpxColorSpace.Unspecified => count == ColourChannels ? JpxDecodeAction.UseRgb : JpxDecodeAction.DoNothing,
        JpxColorSpace.Srgb => count > ColourChannels ? JpxDecodeAction.ConvertArgbToRgb : JpxDecodeAction.UseRgb,
        JpxColorSpace.Gray => JpxDecodeAction.UseGray,
        JpxColorSpace.Cmyk => JpxDecodeAction.UseCmyk,
        _ => JpxDecodeAction.DoNothing,
    };

    /// <summary>Chooses the conversion from the dictionary's colour space, which must agree with the file's for the device spaces.</summary>
    /// <param name="space">The file's colour space.</param>
    /// <param name="count">The channels.</param>
    /// <param name="dictionarySpace">The dictionary's colour space.</param>
    /// <returns>The conversion, or <see langword="null"/> when the two disagree.</returns>
    private static JpxDecodeAction? FromBoth(JpxColorSpace space, int count, PdfColorSpace dictionarySpace)
    {
        var loose = space is JpxColorSpace.Unknown or JpxColorSpace.Unspecified;
        return dictionarySpace.Kind switch
        {
            PdfColorSpaceKind.DeviceGray => loose || space == JpxColorSpace.Gray ? JpxDecodeAction.UseGray : null,
            PdfColorSpaceKind.DeviceRgb when loose || space == JpxColorSpace.Srgb => count > ColourChannels ? JpxDecodeAction.ConvertArgbToRgb : JpxDecodeAction.UseRgb,
            PdfColorSpaceKind.DeviceRgb => null,
            PdfColorSpaceKind.DeviceCmyk => loose || space == JpxColorSpace.Cmyk ? JpxDecodeAction.UseCmyk : null,
            _ => FromOther(space, count, dictionarySpace),
        };
    }

    /// <summary>Chooses the conversion for a dictionary colour space that is not a device space.</summary>
    /// <param name="space">The file's colour space.</param>
    /// <param name="count">The channels.</param>
    /// <param name="dictionarySpace">The dictionary's colour space.</param>
    /// <returns>The conversion.</returns>
    private static JpxDecodeAction FromOther(JpxColorSpace space, int count, PdfColorSpace dictionarySpace)
    {
        // Many iOS-made files pair a three-component space with four sRGB channels (crbug.com/345431077).
        if (dictionarySpace.Components == ColourChannels && count == FourChannels && space == JpxColorSpace.Srgb)
        {
            return JpxDecodeAction.ConvertArgbToRgb;
        }

        return dictionarySpace.Kind == PdfColorSpaceKind.Indexed && dictionarySpace.Components == 1 ? JpxDecodeAction.UseIndexed : JpxDecodeAction.DoNothing;
    }

    /// <summary>Gets the component count PDFium takes from the file when the dictionary has no /ColorSpace.</summary>
    /// <param name="space">The file's colour space.</param>
    /// <param name="count">The channels.</param>
    /// <returns>The component count.</returns>
    private static int ComponentsOf(JpxColorSpace space, int count) => space switch
    {
        JpxColorSpace.Gray => 1,
        JpxColorSpace.Srgb or JpxColorSpace.Sycc or JpxColorSpace.Eycc => ColourChannels,
        JpxColorSpace.Cmyk => FourChannels,
        _ => count,
    };

    /// <summary>A colour space after the sYCC decision, and the conversion chosen.</summary>
    /// <param name="Space">The colour space.</param>
    /// <param name="Sycc">The sYCC conversion, or none.</param>
    private readonly record struct SpaceChoice(JpxColorSpace Space, JpxSyccLayout Sycc);
}
