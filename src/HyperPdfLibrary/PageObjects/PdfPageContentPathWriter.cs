// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Writes paths, paints, clips and page-space transforms.</summary>
public static class PdfPageContentPathWriter
{
    /// <summary>Writes a clipping path on its own, so a deleted or moved path still clips what follows.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "item">The object; only paths that clip write anything.</param>
    internal static void WriteClipOnly(ref PdfContentBuilder builder, PdfPageObject item)
    {
        if (item is not PdfPathObject { Clip: not PdfClipMode.None } path)
        {
            return;
        }

        PdfPageContentPathWriter.WriteSegments(ref builder, path.Segments);
        PdfPageContentPathWriter.WriteClip(ref builder, path.Clip);
        builder.EndPath();
    }

    /// <summary>Writes a path: its segments, its clip when it keeps one, and its painting operator.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "path">The path.</param>
    /// <param name = "withClip">Whether to write the clip too; when not, the caller writes it separately.</param>
    internal static void WritePath(ref PdfContentBuilder builder, PdfPathObject path, bool withClip)
    {
        PdfPageContentPathWriter.WriteSegments(ref builder, path.Segments);
        if (withClip)
        {
            PdfPageContentPathWriter.WriteClip(ref builder, path.Clip);
        }

        switch (path.PaintMode)
        {
            case PdfPathPaintMode.Stroke:
                {
                    PdfPageContentPathWriter.WriteStroke(ref builder, path);
                    break;
                }

            case PdfPathPaintMode.Fill:
                {
                    PdfPageContentPathWriter.WriteFill(ref builder, path);
                    break;
                }

            case PdfPathPaintMode.FillStroke:
                {
                    PdfPageContentPathWriter.WriteFillStroke(ref builder, path);
                    break;
                }

            default:
                {
                    builder.EndPath();
                    break;
                }
        }
    }

    /// <summary>Writes the painting operator of a path that is stroked.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "path">The path.</param>
    internal static void WriteStroke(ref PdfContentBuilder builder, PdfPathObject path)
    {
        if (path.ClosesPath)
        {
            builder.CloseAndStroke();
        }
        else
        {
            builder.Stroke();
        }
    }

    /// <summary>Writes the painting operator of a path that is filled.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "path">The path.</param>
    internal static void WriteFill(ref PdfContentBuilder builder, PdfPathObject path)
    {
        if (path.EvenOddFill)
        {
            builder.FillEvenOdd();
        }
        else
        {
            builder.Fill();
        }
    }

    /// <summary>Writes the painting operator of a path that is filled and stroked.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "path">The path.</param>
    internal static void WriteFillStroke(ref PdfContentBuilder builder, PdfPathObject path)
    {
        if (path.ClosesPath)
        {
            if (path.EvenOddFill)
            {
                builder.CloseFillEvenOddAndStroke();
            }
            else
            {
                builder.CloseFillAndStroke();
            }

            return;
        }

        if (path.EvenOddFill)
        {
            builder.FillEvenOddAndStroke();
        }
        else
        {
            builder.FillAndStroke();
        }
    }

    /// <summary>Writes the clipping operator of a path.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "mode">The clip mode.</param>
    internal static void WriteClip(ref PdfContentBuilder builder, PdfClipMode mode)
    {
        if (mode == PdfClipMode.NonZero)
        {
            builder.Clip();
        }
        else if (mode == PdfClipMode.EvenOdd)
        {
            builder.ClipEvenOdd();
        }
    }

    /// <summary>Writes path segments.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "segments">The segments.</param>
    internal static void WriteSegments(ref PdfContentBuilder builder, PdfPathSegment[] segments)
    {
        foreach (var segment in segments)
        {
            switch (segment.Kind)
            {
                case PdfPathSegmentKind.MoveTo:
                    {
                        builder.MoveTo(segment.X1, segment.Y1);
                        break;
                    }

                case PdfPathSegmentKind.LineTo:
                    {
                        builder.LineTo(segment.X1, segment.Y1);
                        break;
                    }

                case PdfPathSegmentKind.CurveTo:
                    {
                        builder.CurveTo(segment.X1, segment.Y1, segment.X2, segment.Y2, segment.X3, segment.Y3);
                        break;
                    }

                case PdfPathSegmentKind.Rectangle:
                    {
                        builder.Rectangle(segment.X1, segment.Y1, segment.X2, segment.Y2);
                        break;
                    }

                case PdfPathSegmentKind.Close:
                    {
                        builder.ClosePath();
                        break;
                    }

                default:
                    {
                        break;
                    }
            }
        }
    }
}
