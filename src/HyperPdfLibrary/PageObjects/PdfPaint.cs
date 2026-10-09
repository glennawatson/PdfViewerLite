// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>A colour as a content stream sets it: a colour space, its components and, for patterns, the pattern's name.</summary>
/// <param name="ColorSpace">The colour space name: a device space, or the name of a resource.</param>
/// <param name="Components">The components, empty for a pattern with no underlying colour.</param>
/// <param name="Pattern">The pattern resource name, or none.</param>
[DebuggerDisplay("PdfPaint: {ColorSpace} with {Components.Length} components")]
public sealed record PdfPaint(PdfName ColorSpace, float[] Components, PdfName Pattern)
{
    /// <summary>The bits the red channel of 0xRRGGBB is shifted left by.</summary>
    private const int RedShift = 16;

    /// <summary>The bits the green channel of 0xRRGGBB is shifted left by.</summary>
    private const int GreenShift = 8;

    /// <summary>The mask of one 8-bit channel.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>The highest 8-bit channel value, as a float.</summary>
    private const float ChannelMax = 255F;

    /// <summary>Gets the initial colour: black in DeviceGray.</summary>
    public static PdfPaint Black { get; } = FromGray(0);

    /// <summary>Gets a value indicating whether the colour is DeviceGray, DeviceRGB or DeviceCMYK.</summary>
    public bool IsDeviceColor => ColorSpace.ToKnownName() is KnownName.DeviceGray or KnownName.DeviceRGB or KnownName.DeviceCMYK;

    /// <summary>Creates a DeviceGray colour.</summary>
    /// <param name="gray">The grey level from 0 (black) to 1 (white).</param>
    /// <returns>The colour.</returns>
    public static PdfPaint FromGray(float gray) => new(KnownName.DeviceGray, [gray], default);

    /// <summary>Creates a DeviceRGB colour.</summary>
    /// <param name="red">The red level from 0 to 1.</param>
    /// <param name="green">The green level from 0 to 1.</param>
    /// <param name="blue">The blue level from 0 to 1.</param>
    /// <returns>The colour.</returns>
    public static PdfPaint FromRgb(float red, float green, float blue) => new(KnownName.DeviceRGB, [red, green, blue], default);

    /// <summary>Creates a DeviceCMYK colour.</summary>
    /// <param name="cyan">The cyan level from 0 to 1.</param>
    /// <param name="magenta">The magenta level from 0 to 1.</param>
    /// <param name="yellow">The yellow level from 0 to 1.</param>
    /// <param name="black">The black level from 0 to 1.</param>
    /// <returns>The colour.</returns>
    public static PdfPaint FromCmyk(float cyan, float magenta, float yellow, float black) => new(KnownName.DeviceCMYK, [cyan, magenta, yellow, black], default);

    /// <summary>Creates a DeviceRGB colour from 0xRRGGBB.</summary>
    /// <param name="rgb">The colour.</param>
    /// <returns>The DeviceRGB colour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfPaint FromRgb24(uint rgb) =>
        FromRgb(((rgb >> RedShift) & ChannelMask) / ChannelMax, ((rgb >> GreenShift) & ChannelMask) / ChannelMax, (rgb & ChannelMask) / ChannelMax);

    /// <summary>Gets the initial colour of a colour space.</summary>
    /// <param name="space">The colour space name.</param>
    /// <returns>The space's initial colour.</returns>
    internal static PdfPaint Initial(PdfName space) => space.ToKnownName() switch
    {
        KnownName.DeviceGray => FromGray(0),
        KnownName.DeviceRGB => FromRgb(0, 0, 0),
        KnownName.DeviceCMYK => FromCmyk(0, 0, 0, 1),
        _ => new(space, [], default),
    };
}
