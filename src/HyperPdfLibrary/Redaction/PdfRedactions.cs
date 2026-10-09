// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Redaction;

/// <summary>
/// Marks areas of a page for redaction with redact annotations (ISO 32000-2, 12.5.6.23) and lists them. Marking removes
/// nothing: the content stays until <see cref="PdfRedactor"/> applies the marks.
/// </summary>
public static class PdfRedactions
{
    /// <summary>The numbers in one quadrilateral of <c>/QuadPoints</c>.</summary>
    private const int QuadNumbers = 8;

    /// <summary>The offset of the x of a quadrilateral's second point.</summary>
    private const int SecondX = 2;

    /// <summary>The offset of the y of a quadrilateral's second point.</summary>
    private const int SecondY = 3;

    /// <summary>The offset of the x of a quadrilateral's third point.</summary>
    private const int ThirdX = 4;

    /// <summary>The offset of the y of a quadrilateral's third point.</summary>
    private const int ThirdY = 5;

    /// <summary>The offset of the x of a quadrilateral's fourth point.</summary>
    private const int FourthX = 6;

    /// <summary>The offset of the y of a quadrilateral's fourth point.</summary>
    private const int FourthY = 7;

    /// <summary>The width of the preview outline in points.</summary>
    private const float OutlineWidth = 1.5F;

    /// <summary>The opacity of the preview tint.</summary>
    private const float TintOpacity = 0.25F;

    /// <summary>The entries of a small dictionary.</summary>
    private const int SmallEntries = 4;

    /// <summary>The first red component of the preview colour.</summary>
    private const float PreviewRed = 1;

    /// <summary>Gets the name of the preview's tint graphics state.</summary>
    private static ReadOnlySpan<byte> TintName => "Tint"u8;

    /// <summary>Gets the name of the preview's solid graphics state.</summary>
    private static ReadOnlySpan<byte> SolidName => "Solid"u8;

    /// <summary>Marks areas of a page for redaction.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="regions">The areas in user space, one quadrilateral each.</param>
    /// <param name="appearance">How the areas look once applied.</param>
    /// <returns>The new annotation's index in the page's <c>/Annots</c> array, or -1 when the page cannot take it.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    /// <exception cref="ArgumentException"><paramref name="regions"/> is empty.</exception>
    public static int Add(PdfDocument document, int pageIndex, ReadOnlySpan<PdfRectangle> regions, PdfRedactionAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(document);
        var store = document.Objects;
        var page = PdfDocumentPages.GetPage(document, pageIndex);
        var annotation = CreateAnnotation(store, regions, appearance);
        var index = PdfPageAnnotations.Append(store, page, annotation);
        if (index >= 0)
        {
            PdfDocumentEditing.InvalidateCaches(document);
            PdfDocumentPageContent.InvalidatePageContent(document);
        }

        return index;
    }

    /// <summary>Creates a redact annotation, with the preview appearance that shows the marked areas, without adding it to a page.</summary>
    /// <param name="store">The document the annotation belongs to.</param>
    /// <param name="regions">The areas in user space, one quadrilateral each.</param>
    /// <param name="appearance">How the areas look once applied.</param>
    /// <returns>The annotation dictionary.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="regions"/> is empty.</exception>
    public static PdfDictionary CreateAnnotation(PdfObjectStore store, ReadOnlySpan<PdfRectangle> regions, PdfRedactionAppearance appearance)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(appearance);
        if (regions.IsEmpty)
        {
            throw new ArgumentException("At least one area is needed.", nameof(regions));
        }

        var annotation = PdfAnnotations.Create(store, KnownName.Redact, Union(regions));
        ArgumentNullException.ThrowIfNull(appearance);
        PdfAnnotations.SetFlags(annotation, PdfAnnotationFlags.Print);
        WriteRegions(store, annotation, regions);
        WriteAppearanceEntries(annotation, appearance);
        return annotation;
    }

    /// <summary>Moves a redact annotation to new areas, drawing the preview again.</summary>
    /// <param name="store">The document the annotation belongs to.</param>
    /// <param name="annotation">The annotation, a copy that is put back afterwards.</param>
    /// <param name="regions">The new areas in user space.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="regions"/> is empty.</exception>
    public static void SetRegions(PdfObjectStore store, PdfDictionary annotation, ReadOnlySpan<PdfRectangle> regions)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(annotation);
        if (regions.IsEmpty)
        {
            throw new ArgumentException("At least one area is needed.", nameof(regions));
        }

        PdfAnnotations.SetRectangle(annotation, Union(regions));
        WriteRegions(store, annotation, regions);
    }

    /// <summary>Lists the redact annotations on a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving them, in annotation order.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    public static void Get(PdfDocument document, int pageIndex, List<PdfRedaction> output)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(output);
        var page = PdfDocumentPages.GetPage(document, pageIndex);
        if (PdfPageAnnotations.GetArray(document.Objects, page) is not { } annotations)
        {
            return;
        }

        for (var i = 0; i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is { } annotation && annotation.IsName(KnownName.Subtype, KnownName.Redact))
            {
                output.Add(Read(pageIndex, i, annotation));
            }
        }
    }

    /// <summary>Lists the redact annotations on every page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="output">The list receiving them, page by page.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void GetAll(PdfDocument document, List<PdfRedaction> output)
    {
        ArgumentNullException.ThrowIfNull(document);
        for (var i = 0; i < document.PageCount; i++)
        {
            Get(document, i, output);
        }
    }

    /// <summary>Takes a redact annotation off a page without applying it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="annotationIndex">The annotation's index in <c>/Annots</c>.</param>
    /// <returns><see langword="true"/> when removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page.</exception>
    public static bool Remove(PdfDocument document, int pageIndex, int annotationIndex)
    {
        ArgumentNullException.ThrowIfNull(document);
        var page = PdfDocumentPages.GetPage(document, pageIndex);
        if (PdfPageAnnotations.Get(document.Objects, page, annotationIndex) is not { } annotation || !annotation.IsName(KnownName.Subtype, KnownName.Redact))
        {
            return false;
        }

        var removed = PdfPageAnnotations.RemoveAt(document.Objects, page, annotationIndex);
        if (removed)
        {
            PdfDocumentEditing.InvalidateCaches(document);
            PdfDocumentPageContent.InvalidatePageContent(document);
        }

        return removed;
    }

    /// <summary>Reads the areas and look of a redact annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The areas: each quadrilateral's bounds, or the annotation's rectangle when it has none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static PdfRectangle[] GetRegions(PdfDictionary annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (annotation.GetArray(KnownName.QuadPoints) is { } quads && quads.Count >= QuadNumbers)
        {
            var regions = new List<PdfRectangle>(quads.Count / QuadNumbers);
            for (var start = 0; start + QuadNumbers <= quads.Count; start += QuadNumbers)
            {
                regions.Add(QuadBounds(quads, start));
            }

            return [.. regions];
        }

        return annotation.TryGetRectangle(KnownName.Rect, out var rectangle) ? [rectangle] : [];
    }

    /// <summary>Reads how a redact annotation looks once applied.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The appearance.</returns>
    internal static PdfRedactionAppearance ReadAppearance(PdfDictionary annotation)
    {
        uint? fill = null;
        if (annotation.GetArray(KnownName.IC) is { Count: > 0 } && PdfAnnotations.TryGetColor(annotation, KnownName.IC, out var color))
        {
            fill = color;
        }

        var overlay = PdfAnnotations.GetText(annotation, OverlayTextName(annotation));
        var repeat = annotation.GetBoolean(KnownName.Repeat);
        var alignment = (PdfRedactionAlignment)Math.Clamp(annotation.GetInt32(KnownName.Q), 0, (int)PdfRedactionAlignment.Right);
        DefaultAppearance.Read(PdfAnnotations.GetText(annotation, KnownName.DA), out var size, out var textColor);
        return new(fill, overlay, repeat, size, textColor, alignment);
    }

    /// <summary>Gets the interned <c>/OverlayText</c> key.</summary>
    /// <param name="annotation">An annotation of the document.</param>
    /// <returns>The key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfName OverlayTextName(PdfDictionary annotation) => annotation.Owner!.Names.Intern("OverlayText"u8);

    /// <summary>Gets the smallest rectangle around some areas.</summary>
    /// <param name="regions">The areas.</param>
    /// <returns>The union.</returns>
    private static PdfRectangle Union(ReadOnlySpan<PdfRectangle> regions)
    {
        var bounds = regions[0];
        foreach (var region in regions)
        {
            bounds = bounds.Union(region);
        }

        return bounds;
    }

    /// <summary>Writes the quadrilaterals and the preview appearance of a redact annotation.</summary>
    /// <param name="store">The document.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="regions">The areas.</param>
    private static void WriteRegions(PdfObjectStore store, PdfDictionary annotation, ReadOnlySpan<PdfRectangle> regions)
    {
        annotation.Set(KnownName.QuadPoints, PdfValue.FromArray(CreateQuads(store, regions)));
        var preview = store.Add(PdfValue.FromStream(CreatePreview(store, regions, Union(regions))));
        var normal = new PdfDictionary(store, 1);
        normal.Set(KnownName.N, PdfValue.FromReference(preview));
        annotation.Set(KnownName.AP, PdfValue.FromDictionary(normal));
    }

    /// <summary>Reads one redact annotation.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The redaction.</returns>
    private static PdfRedaction Read(int pageIndex, int index, PdfDictionary annotation) =>
        new(pageIndex, index, GetRegions(annotation), ReadAppearance(annotation));

    /// <summary>Gets the bounds of one quadrilateral of <c>/QuadPoints</c>.</summary>
    /// <param name="quads">The array.</param>
    /// <param name="start">The index of the quadrilateral's first number.</param>
    /// <returns>The bounds.</returns>
    private static PdfRectangle QuadBounds(PdfArray quads, int start)
    {
        var left = MathF.Min(MathF.Min(quads.GetSingle(start), quads.GetSingle(start + SecondX)), MathF.Min(quads.GetSingle(start + ThirdX), quads.GetSingle(start + FourthX)));
        var right = MathF.Max(MathF.Max(quads.GetSingle(start), quads.GetSingle(start + SecondX)), MathF.Max(quads.GetSingle(start + ThirdX), quads.GetSingle(start + FourthX)));
        var bottom = MathF.Min(MathF.Min(quads.GetSingle(start + 1), quads.GetSingle(start + SecondY)), MathF.Min(quads.GetSingle(start + ThirdY), quads.GetSingle(start + FourthY)));
        var top = MathF.Max(MathF.Max(quads.GetSingle(start + 1), quads.GetSingle(start + SecondY)), MathF.Max(quads.GetSingle(start + ThirdY), quads.GetSingle(start + FourthY)));
        return new(left, bottom, right, top);
    }

    /// <summary>Writes <c>/QuadPoints</c>: top left, top right, bottom left, bottom right for each area.</summary>
    /// <param name="store">The document.</param>
    /// <param name="regions">The areas.</param>
    /// <returns>The array.</returns>
    private static PdfArray CreateQuads(PdfObjectStore store, ReadOnlySpan<PdfRectangle> regions)
    {
        var quads = new float[regions.Length * QuadNumbers];
        for (var i = 0; i < regions.Length; i++)
        {
            var r = regions[i];
            float[] corners = [r.Left, r.Top, r.Right, r.Top, r.Left, r.Bottom, r.Right, r.Bottom];
            corners.CopyTo(quads, i * QuadNumbers);
        }

        return PdfArray.FromNumbers(store, quads);
    }

    /// <summary>Writes the optional entries that say how the areas look once applied.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="appearance">The appearance.</param>
    private static void WriteAppearanceEntries(PdfDictionary annotation, PdfRedactionAppearance appearance)
    {
        if (appearance.FillColor is { } fill)
        {
            PdfAnnotations.SetColor(annotation, KnownName.IC, fill);
        }

        if (appearance.OverlayText.Length > 0)
        {
            PdfAnnotations.SetText(annotation, OverlayTextName(annotation), appearance.OverlayText);
            annotation.Set(KnownName.Repeat, PdfValue.FromBoolean(appearance.Repeat));
        }

        annotation.Set(KnownName.Q, PdfValue.FromInteger((int)appearance.Alignment));
        PdfAnnotations.SetText(annotation, KnownName.DA, DefaultAppearance.Write(appearance.FontSize, appearance.TextColor));
    }

    /// <summary>Draws the marked areas: a pale red tint with a red outline, so the content under them stays readable.</summary>
    /// <param name="store">The document.</param>
    /// <param name="regions">The areas.</param>
    /// <param name="bounds">The annotation's rectangle.</param>
    /// <returns>The form XObject.</returns>
    private static PdfStream CreatePreview(PdfObjectStore store, ReadOnlySpan<PdfRectangle> regions, PdfRectangle bounds)
    {
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SetGraphicsState(TintName);
            builder.SetFillRgb(PreviewRed, 0, 0);
            foreach (var region in regions)
            {
                builder.Rectangle(region.Left, region.Bottom, region.Width, region.Height);
                builder.Fill();
            }

            builder.SetGraphicsState(SolidName);
            builder.SetStrokeRgb(PreviewRed, 0, 0);
            builder.SetLineWidth(OutlineWidth);
            foreach (var region in regions)
            {
                builder.Rectangle(region.Left, region.Bottom, region.Width, region.Height);
                builder.Stroke();
            }

            return builder.ToFormXObject(store, bounds, CreatePreviewResources(store));
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Creates the graphics states the preview uses.</summary>
    /// <param name="store">The document.</param>
    /// <returns>The resources.</returns>
    private static PdfDictionary CreatePreviewResources(PdfObjectStore store)
    {
        var tint = new PdfDictionary(store, SmallEntries);
        tint.Set(KnownName.Type, PdfValue.FromName(KnownName.ExtGState));
        var lowerCa = store.Names.Intern("ca"u8);
        tint.Set(lowerCa, PdfValue.FromReal(TintOpacity));
        var solid = new PdfDictionary(store, SmallEntries);
        solid.Set(KnownName.Type, PdfValue.FromName(KnownName.ExtGState));
        solid.Set(lowerCa, PdfValue.FromInteger(1));
        solid.Set(KnownName.CA, PdfValue.FromInteger(1));
        var states = new PdfDictionary(store, SmallEntries);
        states.Set(store.Names.Intern(TintName), PdfValue.FromDictionary(tint));
        states.Set(store.Names.Intern(SolidName), PdfValue.FromDictionary(solid));
        var resources = new PdfDictionary(store, 1);
        resources.Set(KnownName.ExtGState, PdfValue.FromDictionary(states));
        return resources;
    }
}
