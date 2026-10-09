// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Draws appearances for annotations that have no normal appearance, following PDFium's CPDF_GenerateAP: Square, Circle,
/// Highlight, Underline, StrikeOut, Squiggly, Ink, Text and FreeText, and text and choice widgets when the form asks for
/// appearances. Line, Polygon and PolyLine, which PDFium leaves blank, are drawn too. Appearances live in memory only.
/// </summary>
internal static partial class AnnotationAppearance
{
    /// <summary>The numbers in one quadrilateral of /QuadPoints.</summary>
    private const int QuadNumbers = 8;

    /// <summary>The index in a quadrilateral of the x PDFium takes as the left (the third point's x).</summary>
    private const int QuadLeft = 4;

    /// <summary>The index in a quadrilateral of the y PDFium takes as the bottom (the third point's y).</summary>
    private const int QuadBottom = 5;

    /// <summary>The index in a quadrilateral of the x PDFium takes as the right (the second point's x).</summary>
    private const int QuadRight = 2;

    /// <summary>The index in a quadrilateral of the y PDFium takes as the top (the second point's y).</summary>
    private const int QuadTop = 3;

    /// <summary>The position of the width in a /Border array.</summary>
    private const int BorderWidthSlot = 2;

    /// <summary>The length of a /Border array that holds a dash array.</summary>
    private const int BorderWithDash = 4;

    /// <summary>The position of the dash array in a /Border array.</summary>
    private const int BorderDashSlot = 3;

    /// <summary>The most dash lengths PDFium writes.</summary>
    private const int MaxDashes = 10;

    /// <summary>Half, to centre a line on an edge.</summary>
    private const float Half = 0.5F;

    /// <summary>The drawers by annotation subtype.</summary>
    private static readonly Dictionary<KnownName, Func<AnnotationContext, GeneratedAppearance?>> Drawers = new()
    {
        [KnownName.Square] = DrawSquare,
        [KnownName.Circle] = DrawCircle,
        [KnownName.Highlight] = DrawHighlight,
        [KnownName.Underline] = DrawUnderline,
        [KnownName.StrikeOut] = DrawStrikeOut,
        [KnownName.Squiggly] = DrawSquiggly,
        [KnownName.Ink] = DrawInk,
        [KnownName.Text] = DrawTextIcon,
        [KnownName.FreeText] = DrawFreeText,
        [KnownName.Line] = DrawLine,
        [KnownName.Polygon] = static context => DrawPolygon(context, true),
        [KnownName.PolyLine] = static context => DrawPolygon(context, false),
        [KnownName.Widget] = DrawWidget,
    };

    /// <summary>Draws an appearance for an annotation that has no normal appearance.</summary>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance, or null when the subtype is not drawn or the annotation lacks what it needs.</returns>
    internal static GeneratedAppearance? Generate(AnnotationContext context)
    {
        var subtype = context.Annotation.GetName(KnownName.Subtype).ToKnownName();
        return Drawers.TryGetValue(subtype, out var drawer) ? drawer(context) : null;
    }

    /// <summary>Reads the border width as PDFium does: /BS /W, else the third entry of /Border, else 1.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The width.</returns>
    private static float BorderWidth(PdfDictionary annotation)
    {
        if (annotation.GetDictionary(KnownName.BS) is { } style && style.ContainsKey(KnownName.W))
        {
            return style.GetSingle(KnownName.W);
        }

        return annotation.GetArray(KnownName.Border) is { Count: > BorderWidthSlot } border ? border.GetSingle(BorderWidthSlot) : 1;
    }

    /// <summary>Writes the dash pattern as PDFium does: /BS /D when the style is dashed, else the fourth entry of /Border.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="annotation">The annotation.</param>
    private static void WriteDash(ref PdfContentBuilder builder, PdfDictionary annotation)
    {
        var dashes = annotation.GetArray(KnownName.Border) is { Count: BorderWithDash } border ? border.GetArray(BorderDashSlot) : null;
        if (annotation.GetDictionary(KnownName.BS) is { } style && style.IsName(KnownName.S, KnownName.D))
        {
            dashes = style.GetArray(KnownName.D);
        }

        if (dashes is not { Count: > 0 })
        {
            return;
        }

        Span<float> lengths = stackalloc float[MaxDashes];
        var count = Math.Min(dashes.Count, MaxDashes);
        for (var i = 0; i < count; i++)
        {
            lengths[i] = dashes.GetSingle(i);
        }

        builder.SetDash(lengths[..count], 0);
    }

    /// <summary>Writes a colour from an annotation entry, or a default when the entry is missing.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="color">The colour array, or null.</param>
    /// <param name="fallbackRgb">The default as 0xRRGGBB, or null to write nothing.</param>
    /// <param name="stroke">Whether to set the stroke colour.</param>
    private static void WriteColor(ref PdfContentBuilder builder, PdfArray? color, uint? fallbackRgb, bool stroke)
    {
        if (color is not null)
        {
            _ = FormAppearance.TryWriteColor(ref builder, color, stroke);
            return;
        }

        if (fallbackRgb is not { } rgb)
        {
            return;
        }

        const int redShift = 16;
        const int greenShift = 8;
        const float channel = 255;
        var red = ((rgb >> redShift) & byte.MaxValue) / channel;
        var green = ((rgb >> greenShift) & byte.MaxValue) / channel;
        var blue = (rgb & byte.MaxValue) / channel;
        if (stroke)
        {
            builder.SetStrokeRgb(red, green, blue);
        }
        else
        {
            builder.SetFillRgb(red, green, blue);
        }
    }

    /// <summary>Gets the rectangle PDFium takes from one quadrilateral of /QuadPoints.</summary>
    /// <param name="points">The /QuadPoints array.</param>
    /// <param name="index">The quadrilateral.</param>
    /// <returns>The normalised rectangle.</returns>
    private static PdfRectangle QuadRectangle(PdfArray points, int index)
    {
        var start = index * QuadNumbers;
        return PdfRectangle.FromCorners(points.GetSingle(start + QuadLeft), points.GetSingle(start + QuadBottom), points.GetSingle(start + QuadRight), points.GetSingle(start + QuadTop));
    }

    /// <summary>Gets the rectangle round every quadrilateral, which PDFium fits a generated markup appearance to.</summary>
    /// <param name="points">The /QuadPoints array.</param>
    /// <returns>The rectangle, or an empty one when there are none.</returns>
    private static PdfRectangle QuadBounds(PdfArray points)
    {
        var count = points.Count / QuadNumbers;
        if (count == 0)
        {
            return default;
        }

        var bounds = QuadRectangle(points, 0);
        for (var i = 1; i < count; i++)
        {
            bounds = bounds.Union(QuadRectangle(points, i));
        }

        return bounds;
    }

    /// <summary>Gets an annotation's normalised /Rect.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The rectangle, or an empty one.</returns>
    private static PdfRectangle Rect(PdfDictionary annotation) =>
        annotation.TryGetRectangle(KnownName.Rect, out var rect) ? PdfRectangle.FromCorners(rect.Left, rect.Bottom, rect.Right, rect.Top) : default;

    /// <summary>Shrinks a rectangle on every side.</summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="amount">How far to move each side in.</param>
    /// <returns>The smaller rectangle.</returns>
    private static PdfRectangle Deflate(PdfRectangle rect, float amount) =>
        new(rect.Left + amount, rect.Bottom + amount, rect.Right - amount, rect.Top - amount);

    /// <summary>Builds the resources PDFium gives a generated appearance: one graphics state with the annotation's opacity and a blend mode.</summary>
    /// <param name="context">The annotation.</param>
    /// <param name="blend">The blend mode name.</param>
    /// <returns>The resources.</returns>
    private static PdfDictionary CreateResources(AnnotationContext context, KnownName blend)
    {
        var store = context.Store;
        var opacityValue = context.Annotation.Get(KnownName.CA);
        var opacity = PdfValue.FromReal(opacityValue.IsNull ? 1 : opacityValue.AsSingle(1));
        var state = new PdfDictionary(store);
        state.Set(KnownName.Type, PdfValue.FromName(KnownName.ExtGState));
        state.Set(KnownName.CA, opacity);
        state.Set(context.Cache.LowerCa, opacity);
        state.Set(KnownName.BM, PdfValue.FromName(blend));
        var states = new PdfDictionary(store);
        states.Set(context.Cache.GraphicsStateName, PdfValue.FromDictionary(state));
        var resources = new PdfDictionary(store);
        resources.Set(KnownName.ExtGState, PdfValue.FromDictionary(states));
        return resources;
    }

    /// <summary>Starts an appearance by selecting its graphics state, as every PDFium appearance does.</summary>
    /// <param name="builder">The content.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Begin(ref PdfContentBuilder builder) => builder.SetGraphicsState("GS"u8);

    /// <summary>Wraps content in an in-memory Form XObject whose box is the rectangle it is fitted to.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The annotation.</param>
    /// <param name="rect">The box, in page space.</param>
    /// <param name="resources">The resources.</param>
    /// <returns>The appearance.</returns>
    private static GeneratedAppearance Finish(ref PdfContentBuilder builder, AnnotationContext context, PdfRectangle rect, PdfDictionary resources) =>
        new(CreateForm(builder.ToArray(), context, rect, resources), null, rect);

    /// <summary>Creates an uncompressed in-memory Form XObject.</summary>
    /// <param name="content">The content.</param>
    /// <param name="context">The annotation.</param>
    /// <param name="box">The /BBox.</param>
    /// <param name="resources">The resources, or null.</param>
    /// <returns>The stream.</returns>
    private static PdfStream CreateForm(byte[] content, AnnotationContext context, PdfRectangle box, PdfDictionary? resources)
    {
        var dictionary = new PdfDictionary(context.Store);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.XObject));
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Form));
        dictionary.Set(KnownName.BBox, PdfValue.FromArray(box.ToArray(context.Store)));
        if (resources is not null)
        {
            dictionary.Set(KnownName.Resources, PdfValue.FromDictionary(resources));
        }

        return new(dictionary, content);
    }
}
