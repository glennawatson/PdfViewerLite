// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Functions;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace HyperPdfLibrary.Content;

/// <content>Graphics state parameter dictionaries (<c>gs</c>) and soft masks.</content>
internal sealed partial class ContentInterpreter
{
    /// <summary>The entries of a transfer function table.</summary>
    private const int TransferEntries = 256;

    /// <summary>The largest byte as a float.</summary>
    private const float ByteMax = 255;

    /// <summary>Half the width and height of the area assumed for a soft mask group that gives no bounding box.</summary>
    private const float UnboundedExtent = 1_000_000;

    /// <summary>The width and height of the area assumed for a soft mask group that gives no bounding box.</summary>
    private const float UnboundedSize = 2 * UnboundedExtent;

    /// <summary>The numbers in a dash array entry of /D plus its phase.</summary>
    private const int DashEntryCount = 2;

    /// <summary>The numbers in an ExtGState /Font entry: the font and its size.</summary>
    private const int FontEntryCount = 2;

    /// <summary>Reads a /BM value: a name, or an array of names of which the first known one applies.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The blend mode.</returns>
    private static PdfBlendMode ReadBlendMode(PdfValue value)
    {
        if (value.AsArray() is not { } names)
        {
            return BlendModeFor(value.AsName()) ?? PdfBlendMode.Normal;
        }

        for (var i = 0; i < names.Count; i++)
        {
            if (BlendModeFor(names.GetName(i)) is { } mode)
            {
                return mode;
            }
        }

        return PdfBlendMode.Normal;
    }

    /// <summary>Maps a blend mode name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The mode, or null for an unknown name.</returns>
    private static PdfBlendMode? BlendModeFor(PdfName name) => name.ToKnownName() switch
    {
        KnownName.Normal or KnownName.Compatible => PdfBlendMode.Normal,
        KnownName.Multiply => PdfBlendMode.Multiply,
        KnownName.Screen => PdfBlendMode.Screen,
        KnownName.Overlay => PdfBlendMode.Overlay,
        KnownName.Darken => PdfBlendMode.Darken,
        KnownName.Lighten => PdfBlendMode.Lighten,
        KnownName.ColorDodge => PdfBlendMode.ColorDodge,
        KnownName.ColorBurn => PdfBlendMode.ColorBurn,
        KnownName.HardLight => PdfBlendMode.HardLight,
        KnownName.SoftLight => PdfBlendMode.SoftLight,
        KnownName.Difference => PdfBlendMode.Difference,
        KnownName.Exclusion => PdfBlendMode.Exclusion,
        KnownName.Hue => PdfBlendMode.Hue,
        KnownName.Saturation => PdfBlendMode.Saturation,
        KnownName.Color => PdfBlendMode.Color,
        KnownName.Luminosity => PdfBlendMode.Luminosity,
        _ => null,
    };

    /// <summary>Reads a boolean entry the way PDFium does, which also accepts a non-zero number.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for <c>true</c> or a non-zero number.</returns>
    private static bool IsTrue(PdfValue value) => value.AsBoolean() || value.AsInt32() != 0;

    /// <summary>Reads the /TR transfer function of a luminosity mask into a lookup table.</summary>
    /// <param name="mask">The soft mask dictionary.</param>
    /// <returns>The table, or null for the identity.</returns>
    private static byte[]? ReadTransfer(PdfDictionary mask)
    {
        var value = mask.Get(KnownName.TR);
        if (value.IsNull || value.IsName(KnownName.Identity) || PdfFunction.Parse(value) is not { } function)
        {
            return null;
        }

        var table = new byte[TransferEntries];
        Span<float> input = stackalloc float[1];
        Span<float> output = stackalloc float[PdfFunction.MaxComponents];
        for (var i = 0; i < table.Length; i++)
        {
            input[0] = i / ByteMax;
            function.Evaluate(input, output);
            table[i] = (byte)MathF.Round(Math.Clamp(output[0], 0, 1) * ByteMax);
        }

        return table;
    }

    /// <summary>Applies the entries of an ExtGState dictionary that affect rendering; rendering intent, flatness and the like are ignored.</summary>
    /// <param name="dictionary">The dictionary.</param>
    private void ApplyExtGState(PdfDictionary dictionary)
    {
        for (var i = 0; i < dictionary.Count; i++)
        {
            var key = dictionary.GetKeyAt(i);
            var value = dictionary.Get(key);
            if (key == _cache.LowerCa)
            {
                _state.FillAlpha = Math.Clamp(value.AsSingle(1), 0, 1);
                continue;
            }

            ApplyExtGStateEntry(key.ToKnownName(), value);
        }

        ApplyOverprint(dictionary);
    }

    /// <summary>
    /// Applies /OP, /op, /OPM and /TK as PDFium reads them: /OP sets stroke overprint, and fill overprint too unless /op
    /// is present. Rendering intent (/RI), flatness (/FL), smoothness (/SM), transfer and halftones are parsed and ignored.
    /// </summary>
    /// <param name="dictionary">The ExtGState dictionary.</param>
    private void ApplyOverprint(PdfDictionary dictionary)
    {
        var stroke = dictionary.Get(KnownName.OP);
        var fill = dictionary.Get(KnownName.FillOverprint);
        if (!stroke.IsNull)
        {
            _state.StrokeOverprint = IsTrue(stroke);
            if (fill.IsNull)
            {
                _state.FillOverprint = _state.StrokeOverprint;
            }
        }

        if (!fill.IsNull)
        {
            _state.FillOverprint = IsTrue(fill);
        }

        var mode = dictionary.Get(KnownName.OPM);
        if (!mode.IsNull)
        {
            _state.OverprintMode = mode.AsInt32();
        }

        var knockout = dictionary.Get(KnownName.TK);
        if (!knockout.IsNull)
        {
            _state.TextKnockout = IsTrue(knockout);
        }
    }

    /// <summary>Applies one ExtGState entry.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, resolved.</param>
    private void ApplyExtGStateEntry(KnownName key, PdfValue value)
    {
        switch (key)
        {
            case KnownName.LW:
            {
                _state.LineWidth = Math.Max(0, value.AsSingle(1));
                break;
            }

            case KnownName.LC:
            {
                _state.LineCap = value.AsInt32();
                break;
            }

            case KnownName.LJ:
            {
                _state.LineJoin = value.AsInt32();
                break;
            }

            case KnownName.ML:
            {
                _state.MiterLimit = value.AsSingle(_state.MiterLimit);
                break;
            }

            case KnownName.D:
            {
                ApplyDash(value.AsArray());
                break;
            }

            case KnownName.CA:
            {
                _state.StrokeAlpha = Math.Clamp(value.AsSingle(1), 0, 1);
                break;
            }

            default:
            {
                ApplyExtGStateObject(key, value);
                break;
            }
        }
    }

    /// <summary>Applies the entries that name other objects: font, blend mode and soft mask.</summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value, resolved.</param>
    private void ApplyExtGStateObject(KnownName key, PdfValue value)
    {
        switch (key)
        {
            case KnownName.Font:
            {
                if (value.AsArray() is { Count: >= FontEntryCount } font && font.GetDictionary(0) is { } dictionary)
                {
                    _state.Font = _cache.Fonts.Get(dictionary);
                    _state.FontSize = font.GetSingle(1);
                }

                break;
            }

            case KnownName.BM:
            {
                _state.BlendMode = ReadBlendMode(value);
                break;
            }

            case KnownName.SMask:
            {
                _state.SoftMask = value.AsDictionary() is { } mask ? GetSoftMask(mask) : null;
                break;
            }

            default:
            {
                // Overprint and text knockout are read by ApplyOverprint; transfer, halftone, flatness and rendering intent change nothing drawn.
                break;
            }
        }
    }

    /// <summary>Applies a /D entry: <c>[dashArray phase]</c>.</summary>
    /// <param name="array">The array.</param>
    private void ApplyDash(PdfArray? array)
    {
        if (array is not { Count: >= DashEntryCount } || array.GetArray(0) is not { } lengths)
        {
            return;
        }

        Span<float> buffer = stackalloc float[MaxDashEntries];
        var count = Math.Min(lengths.Count, buffer.Length);
        var total = 0F;
        for (var i = 0; i < count; i++)
        {
            buffer[i] = lengths.GetSingle(i);
            total += Math.Abs(buffer[i]);
        }

        _state.Dash = count == 0 || total <= 0 ? [] : [.. buffer[..count]];
        _state.DashPhase = array.GetSingle(1);
    }

    /// <summary>Gets a soft mask recorded with the current transform.</summary>
    /// <param name="mask">The soft mask dictionary.</param>
    /// <returns>The mask, or null when it is damaged.</returns>
    private PdfSoftMask? GetSoftMask(PdfDictionary mask) => mask.GetStream(KnownName.G) is null
        ? null
        : _cache.SoftMasks.GetOrCreate(new(mask, _state.Ctm), new SoftMaskRequest(this, mask), static (key, request) => request.Owner.RecordSoftMask(request.Mask, key.Ctm));

    /// <summary>Records a soft mask's group to a picture.</summary>
    /// <param name="mask">The soft mask dictionary.</param>
    /// <param name="ctm">The transform in force when the mask was set.</param>
    /// <returns>The mask.</returns>
    private PdfSoftMask RecordSoftMask(PdfDictionary mask, Matrix3x2 ctm)
    {
        var group = mask.GetStream(KnownName.G)!;
        var bounds = group.Dictionary.TryGetRectangle(KnownName.BBox, out var box)
            ? SkiaConversions.ToSkMatrix(ctm).MapRect(new(box.Left, box.Bottom, box.Right, box.Top))
            : SKRect.Create(-UnboundedExtent, -UnboundedExtent, UnboundedSize, UnboundedSize);
        using var recorder = _device.CreatePictureDevice(bounds);
        using var child = new ContentInterpreter(_cache, recorder, _depth + 1) { Printing = Printing };
        child.RunForm(group, ctm, CurrentResources());
        var picture = recorder.Finish();
        var luminosity = mask.IsName(KnownName.S, KnownName.Luminosity);
        return new(picture, luminosity, ReadBackdrop(mask, group), luminosity ? ReadTransfer(mask) : null);
    }

    /// <summary>Reads the /BC backdrop colour of a luminosity mask.</summary>
    /// <param name="mask">The soft mask dictionary.</param>
    /// <param name="group">The mask group.</param>
    /// <returns>The backdrop; black when none is given.</returns>
    private SKColor ReadBackdrop(PdfDictionary mask, PdfStream group)
    {
        var colors = mask.GetArray(KnownName.BC);
        if (colors is null)
        {
            return SKColors.Black;
        }

        var groupDictionary = group.Dictionary.GetDictionary(KnownName.Group);
        var space = groupDictionary is null || groupDictionary.Get(KnownName.CS).IsNull
            ? PdfColorSpace.FromComponents(colors.Count)
            : _cache.GetColorSpace(groupDictionary.Get(KnownName.CS), CurrentResources()?.GetDictionary(KnownName.ColorSpace));
        Span<float> components = stackalloc float[PdfColorSpace.MaxComponents];
        _ = colors.ReadNumbers(components);
        var colour = ColorState.Resolve(space, components);
        return new(colour.Red, colour.Green, colour.Blue);
    }
}
