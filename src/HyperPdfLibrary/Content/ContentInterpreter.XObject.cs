// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Syntax;
using SkiaSharp;

namespace HyperPdfLibrary.Content;

/// <content>XObjects, transparency groups, inline images and shadings.</content>
internal sealed partial class ContentInterpreter
{
    /// <summary>The bytes of the <c>&lt;&lt;</c> and <c>&gt;&gt;</c> that wrap an inline image's entries.</summary>
    private const int DictionaryDelimiters = 4;

    /// <summary>The width of one delimiter pair.</summary>
    private const int DelimiterWidth = 2;

    /// <summary>Determines whether an object's optional content is shown.</summary>
    /// <param name="dictionary">The XObject's or annotation's dictionary.</param>
    /// <returns><see langword="true"/> when it has no /OC or its layers are on.</returns>
    internal bool IsVisible(PdfDictionary dictionary)
    {
        var membership = dictionary.GetRaw(KnownName.OC);
        return membership.IsNull || IsLayerShown(membership);
    }

    /// <summary>Handles <c>Do</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpPaintXObject(ContentInterpreter self, ref ContentReader reader)
    {
        if (self.FindResource(KnownName.XObject, reader.Operand(0).Name).AsStream() is { } xobject)
        {
            self.PaintXObject(xobject);
        }
    }

    /// <summary>Handles <c>BI</c> after the reader has found the image's data.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpInlineImage(ContentInterpreter self, ref ContentReader reader)
    {
        // Text extraction never decodes images.
        if (self._hidden == 0 && self._textObjects is null)
        {
            self.DrawInlineImage(ref reader);
        }
    }

    /// <summary>Handles <c>sh</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpPaintShading(ContentInterpreter self, ref ContentReader reader)
    {
        if (self._hidden > 0)
        {
            return;
        }

        var shading = self.GetShading(self.FindResource(KnownName.Shading, reader.Operand(0).Name));
        if (shading is not null)
        {
            self._device.PaintShading(shading, ref self._state);
        }
    }

    /// <summary>
    /// Determines whether optional content is shown: by the viewer's layer switches, or by print usage when printing.
    /// Text extraction sees every layer, as PDFium extracts text from hidden layers too.
    /// </summary>
    /// <param name="membership">The /OC value.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    private bool IsLayerShown(PdfValue membership) =>
        _textObjects is not null || (Printing ? _layers.IsPrinted(membership) : _layers.IsVisible(membership));

    /// <summary>Draws an XObject.</summary>
    /// <param name="xobject">The image or form stream.</param>
    private void PaintXObject(PdfStream xobject)
    {
        var dictionary = xobject.Dictionary;
        if (_hidden > 0 || !IsVisible(dictionary))
        {
            return;
        }

        switch (dictionary.GetName(KnownName.Subtype).ToKnownName())
        {
            case KnownName.Form:
            {
                DrawForm(xobject);
                break;
            }

            case KnownName.Image:
            {
                DrawImageXObject(xobject);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Draws an image XObject into the unit square.</summary>
    /// <param name="image">The image stream.</param>
    private void DrawImageXObject(PdfStream image)
    {
        if (_textObjects is not null)
        {
            // Text extraction never decodes images.
            return;
        }

        var entry = _cache.AcquireImage(image);
        if (entry is null)
        {
            return;
        }

        try
        {
            if (!(entry.IsMask && _state.Fill.PaintsNothing))
            {
                DrawDecodedImage(entry.Image, entry.IsMask, entry.Interpolate, image.Dictionary);
            }
        }
        finally
        {
            entry.Release();
        }
    }

    /// <summary>Draws a decoded image, simulating overprint the way PDFium does.</summary>
    /// <param name="image">The image.</param>
    /// <param name="isMask">Whether it is a stencil mask.</param>
    /// <param name="smooth">Whether it asks for smoothing.</param>
    /// <param name="dictionary">The image dictionary.</param>
    private void DrawDecodedImage(SKImage image, bool isMask, bool smooth, PdfDictionary dictionary)
    {
        if (_colorLocked)
        {
            DrawImageAsShape(image, isMask, smooth);
            return;
        }

        if (isMask || !OverprintsImage(dictionary))
        {
            _device.DrawImage(image, isMask, smooth, ref _state);
            return;
        }

        var state = _state;
        state.BlendMode = PdfBlendMode.Darken;
        _device.DrawImage(image, false, smooth, ref state);
    }

    /// <summary>
    /// Determines whether an image overprints. PDFium's source simulates overprint only here: an opaque, unmasked image
    /// in DeviceCMYK, Separation or DeviceN drawn with fill overprint on, overprint mode 0, the Normal blend and both
    /// alphas at 1 is darkened into the page instead of covering it. Paths, text and shadings never overprint. The PDFium
    /// build the app ships shows no darkening, so the rule applies only when the document's options turn it on.
    /// </summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <returns><see langword="true"/> when the image is drawn with the Darken blend.</returns>
    private bool OverprintsImage(PdfDictionary dictionary)
    {
        if (!_cache.SimulateOverprint || !_state.FillOverprint || _state.OverprintMode != 0 || _state.BlendMode != PdfBlendMode.Normal || _state.FillAlpha < 1 || _state.StrokeAlpha < 1)
        {
            return false;
        }

        if (dictionary.ContainsKey(KnownName.SMask) || dictionary.ContainsKey(KnownName.Mask))
        {
            return false;
        }

        var space = dictionary.Get(KnownName.ColorSpace);
        return IsSubtractive(space.IsNull ? dictionary.Get(KnownName.CS) : space);
    }

    /// <summary>Determines whether a colour space is DeviceCMYK, Separation or DeviceN, following one resource name.</summary>
    /// <param name="space">The colour space value.</param>
    /// <returns><see langword="true"/> for those families.</returns>
    private bool IsSubtractive(PdfValue space)
    {
        if (space.AsArray() is { Count: > 0 } array)
        {
            return array.GetName(0).ToKnownName() is KnownName.Separation or KnownName.DeviceN or KnownName.DeviceCMYK;
        }

        var name = space.AsName();
        if (name.IsNone)
        {
            return false;
        }

        var known = name.ToKnownName();
        if (known is KnownName.DeviceCMYK or KnownName.CMYK)
        {
            return true;
        }

        var resource = FindResource(KnownName.ColorSpace, name);
        return resource.AsArray() is { Count: > 0 } named
            ? named.GetName(0).ToKnownName() is KnownName.Separation or KnownName.DeviceN or KnownName.DeviceCMYK
            : resource.AsName().ToKnownName() == KnownName.DeviceCMYK;
    }

    /// <summary>Decodes and draws an inline image whose ranges the reader has found.</summary>
    /// <param name="reader">The reader.</param>
    private void DrawInlineImage(ref ContentReader reader)
    {
        var content = reader.Content;
        var range = reader.InlineImageDictionary;
        var offset = range.Start.GetOffset(content.Length);
        var length = range.End.GetOffset(content.Length) - offset;
        var text = new byte[length + DictionaryDelimiters];
        text[0] = (byte)'<';
        text[1] = (byte)'<';
        content.Slice(offset, length).CopyTo(text.AsSpan(DelimiterWidth));
        text[length + DelimiterWidth] = (byte)'>';
        text[length + DelimiterWidth + 1] = (byte)'>';
        var parser = new PdfParser(text, 0, _cache.Document.Objects, _cache.Names);
        if (parser.ParseValue().AsDictionary() is not { } dictionary)
        {
            return;
        }

        using var scope = Graphics.Colors.OutputIntentColors.Enter(_cache.DeviceColors);
        var data = PdfImageDecoder.DecodeInline(dictionary, content[reader.InlineImageData], CurrentResources());
        if (data is null || data.UnsupportedCodec != PdfImageCodec.None)
        {
            return;
        }

        using var image = PdfRenderCache.ToSkImage(data);
        if (image is not null && !(data.IsStencilMask && _state.Fill.PaintsNothing))
        {
            DrawDecodedImage(image, data.IsStencilMask, data.Interpolate, dictionary);
        }
    }

    /// <summary>
    /// Draws an image inside a Type 3 glyph that gives only a shape (<c>d1</c>) or an uncoloured pattern. PDFium renders
    /// those as a mask painted with one colour, so a stencil mask keeps its shape and any other image covers its unit square.
    /// </summary>
    /// <param name="image">The image.</param>
    /// <param name="isMask">Whether it is a stencil mask.</param>
    /// <param name="smooth">Whether it asks for smoothing.</param>
    private void DrawImageAsShape(SKImage image, bool isMask, bool smooth)
    {
        if (isMask)
        {
            _device.DrawImage(image, true, smooth, ref _state);
            return;
        }

        PathMove(0, 0);
        PathLine(1, 0);
        PathLine(1, 1);
        PathLine(0, 1);
        _path.Close();
        using var square = _path.Detach();
        _pathPoints = 0;
        _device.Fill(square, false, ref _state);
    }

    /// <summary>Draws a form XObject: its matrix, bounding box, resources and, when needed, its transparency group.</summary>
    /// <param name="form">The form stream.</param>
    private void DrawForm(PdfStream form)
    {
        if (_depth >= PdfLimits.MaxDrawDepth)
        {
            return;
        }

        var dictionary = form.Dictionary;
        var tm = _tm;
        var tlm = _tlm;
        var patternBase = _patternBase;
        _path.Reset();
        _pathPoints = 0;
        _pendingClip = false;
        SaveState();
        _state.Ctm = ReadMatrix(dictionary, KnownName.Matrix) * _state.Ctm;
        var bounds = ClipToBox(dictionary);
        var group = BeginFormGroup(dictionary, bounds);
        _patternBase = _state.Ctm;
        _resources.Add(dictionary.GetDictionary(KnownName.Resources) ?? CurrentResources());
        _depth++;
        try
        {
            RunStreamBody(form);
        }
        finally
        {
            _depth--;
            _resources.RemoveAt(_resources.Count - 1);
            if (group is { } info)
            {
                _device.EndGroup(info);
            }

            RestoreState();
            _tm = tm;
            _tlm = tlm;
            _patternBase = patternBase;
        }
    }

    /// <summary>Clips to a form's /BBox.</summary>
    /// <param name="dictionary">The form dictionary.</param>
    /// <returns>The box in page space, or an empty rectangle when the form has none.</returns>
    private SKRect ClipToBox(PdfDictionary dictionary)
    {
        if (!dictionary.TryGetRectangle(KnownName.BBox, out var box))
        {
            return SKRect.Empty;
        }

        PathMove(box.Left, box.Bottom);
        PathLine(box.Right, box.Bottom);
        PathLine(box.Right, box.Top);
        PathLine(box.Left, box.Top);
        _path.Close();
        using (var clip = _path.Detach())
        {
            _device.Clip(clip, false, _state.Ctm);
        }

        _pathPoints = 0;
        return SkiaConversions.ToSkMatrix(_state.Ctm).MapRect(new(box.Left, box.Bottom, box.Right, box.Top));
    }

    /// <summary>Determines whether the state composites objects with constant alpha, a blend mode or a soft mask.</summary>
    /// <returns><see langword="true"/> when something other than opaque Normal painting applies.</returns>
    private bool HasTransparency() => _state.FillAlpha < 1 || _state.BlendMode != PdfBlendMode.Normal || _state.SoftMask is not null;

    /// <summary>Starts a transparency group when the form is one and is drawn with transparency.</summary>
    /// <param name="dictionary">The form dictionary.</param>
    /// <param name="bounds">The form's box in page space.</param>
    /// <returns>The group, or null when the form is drawn directly.</returns>
    private GroupInfo? BeginFormGroup(PdfDictionary dictionary, SKRect bounds)
    {
        var group = dictionary.GetDictionary(KnownName.Group);
        if (group is null || !group.IsName(KnownName.S, KnownName.Transparency))
        {
            return null;
        }

        // PDFium composites a group separately when it is isolated or drawn with transparency; a knockout group needs its
        // own layer too, so its objects replace each other rather than the page beneath.
        var isolated = group.GetBoolean(KnownName.I);
        var knockout = group.GetBoolean(KnownName.K);
        if (!isolated && !knockout && !HasTransparency())
        {
            return null;
        }

        var info = new GroupInfo(isolated, knockout, _state.FillAlpha, _state.BlendMode, _state.SoftMask, bounds);
        _device.BeginGroup(info);
        _state.FillAlpha = 1;
        _state.StrokeAlpha = 1;
        _state.BlendMode = PdfBlendMode.Normal;
        _state.SoftMask = null;
        return info;
    }
}
