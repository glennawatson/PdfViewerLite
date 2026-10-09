// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <content>The text markup annotations: Highlight, Underline, StrikeOut and Squiggly.</content>
internal static partial class AnnotationAppearance
{
    /// <summary>The default highlight colour, yellow.</summary>
    private const uint Yellow = 0xFFFF00;

    /// <summary>The height and step of a squiggly line's waves.</summary>
    private const float SquiggleDelta = 2;

    /// <summary>Draws a Highlight annotation: each quadrilateral filled with the Multiply blend mode.</summary>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance, or null without quadrilaterals.</returns>
    private static GeneratedAppearance? DrawHighlight(AnnotationContext context)
    {
        if (context.Annotation.GetArray(KnownName.QuadPoints) is not { } points || QuadBounds(points).IsEmpty)
        {
            return null;
        }

        var builder = default(PdfContentBuilder);
        try
        {
            Begin(ref builder);
            WriteColor(ref builder, context.Annotation.GetArray(KnownName.C), Yellow, false);
            for (var i = 0; i < points.Count / QuadNumbers; i++)
            {
                var rect = QuadRectangle(points, i);
                builder.MoveTo(rect.Left, rect.Top);
                builder.LineTo(rect.Right, rect.Top);
                builder.LineTo(rect.Right, rect.Bottom);
                builder.LineTo(rect.Left, rect.Bottom);
                builder.ClosePath();
                builder.Fill();
            }

            return Finish(ref builder, context, QuadBounds(points), CreateResources(context, KnownName.Multiply));
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Draws an Underline annotation: a one point line one point above each quadrilateral's bottom.</summary>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance, or null without quadrilaterals.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GeneratedAppearance? DrawUnderline(AnnotationContext context) =>
        DrawMarkupLines(context, static (ref builder, rect) =>
        {
            builder.MoveTo(rect.Left, rect.Bottom + 1);
            builder.LineTo(rect.Right, rect.Bottom + 1);
            builder.Stroke();
        });

    /// <summary>Draws a StrikeOut annotation: a one point line through the middle of each quadrilateral.</summary>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance, or null without quadrilaterals.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GeneratedAppearance? DrawStrikeOut(AnnotationContext context) =>
        DrawMarkupLines(context, static (ref builder, rect) =>
        {
            var y = (rect.Top + rect.Bottom) * Half;
            builder.MoveTo(rect.Left, y);
            builder.LineTo(rect.Right, y);
            builder.Stroke();
        });

    /// <summary>Draws a Squiggly annotation: a zigzag two points high along each quadrilateral's bottom.</summary>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance, or null without quadrilaterals.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GeneratedAppearance? DrawSquiggly(AnnotationContext context) =>
        DrawMarkupLines(context, static (ref builder, rect) => AddSquiggle(ref builder, rect));

    /// <summary>Draws one line per quadrilateral in the stroke colour, black by default, one point wide.</summary>
    /// <param name="context">The annotation.</param>
    /// <param name="line">Adds and strokes the line of one quadrilateral.</param>
    /// <returns>The appearance, or null without quadrilaterals.</returns>
    private static GeneratedAppearance? DrawMarkupLines(AnnotationContext context, MarkupLine line)
    {
        if (context.Annotation.GetArray(KnownName.QuadPoints) is not { } points || QuadBounds(points).IsEmpty)
        {
            return null;
        }

        var builder = default(PdfContentBuilder);
        try
        {
            Begin(ref builder);
            WriteColor(ref builder, context.Annotation.GetArray(KnownName.C), 0, true);
            builder.SetLineWidth(1);
            for (var i = 0; i < points.Count / QuadNumbers; i++)
            {
                line(ref builder, QuadRectangle(points, i));
            }

            return Finish(ref builder, context, QuadBounds(points), CreateResources(context, KnownName.Normal));
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Adds the zigzag PDFium draws under squiggly text.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="rect">The quadrilateral's rectangle.</param>
    private static void AddSquiggle(ref PdfContentBuilder builder, PdfRectangle rect)
    {
        var top = rect.Bottom + SquiggleDelta;
        var bottom = rect.Bottom;
        builder.MoveTo(rect.Left, top);
        var x = rect.Left + SquiggleDelta;
        var upwards = false;
        while (x < rect.Right)
        {
            builder.LineTo(x, upwards ? top : bottom);
            x += SquiggleDelta;
            upwards = !upwards;
        }

        var remainder = rect.Right - (x - SquiggleDelta);
        builder.LineTo(rect.Right, upwards ? bottom + remainder : top - remainder);
        builder.Stroke();
    }
}
