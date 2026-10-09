// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.HyperPdf;

/// <content>
/// Recolouring, moving, resizing and restyling. Strokes are redrawn point by point; rectangles, ellipses and notes
/// lose their appearance so readers draw them again from their rectangle; stamps and text keep their appearance,
/// which readers fit from its box to the new rectangle.
/// </content>
internal sealed partial class HyperPdfAnnotations
{
    /// <inheritdoc/>
    public bool SetColor(int pageIndex, int index, uint color)
    {
        lock (_gate)
        {
            if (!TryEdit(pageIndex, index, out var page, out var annotation))
            {
                return false;
            }

            var subtype = annotation.GetName(KnownName.Subtype).ToKnownName();
            var changed = subtype switch
            {
                KnownName.Stamp => RecolorAppearance(annotation, color),
                KnownName.FreeText => IsWrittenHere(annotation) && RecolorAppearance(annotation, color),
                KnownName.Ink or KnownName.Polygon or KnownName.PolyLine => RecolorStrokes(annotation, color),
                KnownName.Redact => false,
                _ => RecolorEntries(annotation, color),
            };
            return changed && Commit(pageIndex, page, index, annotation);
        }
    }

    /// <inheritdoc/>
    public bool SetBounds(int pageIndex, int index, PageRect bounds)
    {
        lock (_gate)
        {
            if (!TryEdit(pageIndex, index, out var page, out var annotation) || GetKind(annotation) is not { } kind || !IsEditable(annotation, kind))
            {
                return false;
            }

            var map = new RectangleMap(PdfAnnotations.GetRectangle(annotation), ToUserRectangle(page, bounds));
            if (!Move(annotation, kind, map))
            {
                return false;
            }

            SetModified(annotation);
            return Commit(pageIndex, page, index, annotation);
        }
    }

    /// <inheritdoc/>
    public bool SetLineWidth(int pageIndex, int index, float width)
    {
        if (width <= 0)
        {
            return false;
        }

        lock (_gate)
        {
            if (!TryEdit(pageIndex, index, out var page, out var annotation) || GetKind(annotation) is not { } kind || !IsEditable(annotation, kind))
            {
                return false;
            }

            if (!Restyle(annotation, kind, width))
            {
                return false;
            }

            SetModified(annotation);
            return Commit(pageIndex, page, index, annotation);
        }
    }

    /// <summary>Recolours an annotation readers draw from its entries, dropping the old appearance.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="color">The colour.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private static bool RecolorEntries(PdfDictionary annotation, uint color)
    {
        _ = PdfAnnotations.RemoveAppearance(annotation);
        PdfAnnotations.SetColor(annotation, KnownName.C, color);
        return true;
    }

    /// <summary>Changes a line width: strokes are drawn again, shapes are left for readers to draw.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="width">The width.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    private bool Restyle(PdfDictionary annotation, AnnotationKind kind, float width)
    {
        switch (annotation.GetName(KnownName.Subtype).ToKnownName())
        {
            case KnownName.Ink or KnownName.Polygon or KnownName.PolyLine:
            {
                PdfAnnotations.SetBorderWidth(annotation, width);
                return MoveStrokes(annotation, kind, RectangleMap.Identity);
            }

            case KnownName.Square or KnownName.Circle:
            {
                PdfAnnotations.SetBorderWidth(annotation, width);
                _ = PdfAnnotations.RemoveAppearance(annotation);
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Moves an annotation the way its kind needs.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="map">The move.</param>
    /// <returns><see langword="true"/> when moved.</returns>
    private bool Move(PdfDictionary annotation, AnnotationKind kind, in RectangleMap map)
    {
        switch (annotation.GetName(KnownName.Subtype).ToKnownName())
        {
            case KnownName.Ink or KnownName.Polygon or KnownName.PolyLine:
            {
                return MoveStrokes(annotation, kind, map);
            }

            case KnownName.Square or KnownName.Circle or KnownName.Text:
            {
                PdfAnnotations.SetRectangle(annotation, map.Target);
                _ = PdfAnnotations.RemoveAppearance(annotation);
                return true;
            }

            case KnownName.Redact:
            {
                return MoveRedaction(annotation, map);
            }

            default:
            {
                PdfAnnotations.SetRectangle(annotation, map.Target);
                MoveCallout(annotation, map);
                return true;
            }
        }
    }

    /// <summary>Recolours an annotation whose appearance is kept: every colour it draws with becomes the new one.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="color">The colour.</param>
    /// <returns><see langword="true"/> when its appearance was recoloured.</returns>
    private bool RecolorAppearance(PdfDictionary annotation, uint color)
    {
        if (PdfAnnotations.GetNormalAppearance(annotation) is not { } appearance || PdfAppearanceColors.Recolor(appearance, color) is not { } recolored)
        {
            return false;
        }

        _ = PdfAnnotations.SetNormalAppearance(_store, annotation, recolored);
        PdfAnnotations.SetColor(annotation, KnownName.C, color);
        SetModified(annotation);
        return true;
    }

    /// <summary>Recolours strokes, drawing them again so they still print.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="color">The colour.</param>
    /// <returns><see langword="true"/> when drawn again.</returns>
    private bool RecolorStrokes(PdfDictionary annotation, uint color)
    {
        PdfAnnotations.SetColor(annotation, KnownName.C, color);
        if (!Redraw(annotation, GetKind(annotation) ?? AnnotationKind.Ink, color))
        {
            return false;
        }

        SetModified(annotation);
        return true;
    }

    /// <summary>Determines whether free text was written by this viewer, which keeps its text to lay it out again.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when written here.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsWrittenHere(PdfDictionary annotation) => PdfAnnotations.HasText(annotation, _names.Text);
}
