// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.PageObjects;

/// <content>Writing the content again.</content>
public sealed partial class PdfPageContent
{
    /// <summary>The components of an RGB colour.</summary>
    private const int RgbComponents = 3;

    /// <summary>The components of a CMYK colour.</summary>
    private const int CmykComponents = 4;

    /// <summary>The resources the last regeneration made names for.</summary>
    private readonly List<PendingResource> _generated = [];

    /// <summary>Gets the byte that ends a line.</summary>
    private static ReadOnlySpan<byte> NewLine => "\n"u8;

    /// <summary>Writes the content again. Objects nobody changed keep the bytes they were read from.</summary>
    /// <returns>The new content stream data, not compressed.</returns>
    /// <exception cref="InvalidOperationException">A form this content paints was changed but its content cannot be written.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] Regenerate() => Regenerate(PdfRegenerateMode.Preserve);

    /// <summary>Writes the content again.</summary>
    /// <param name="mode">Whether objects nobody changed keep their original bytes or are written from the model.</param>
    /// <returns>The new content stream data, not compressed.</returns>
    /// <exception cref="InvalidOperationException">A form this content paints was changed but its content cannot be written.</exception>
    public byte[] Regenerate(PdfRegenerateMode mode)
    {
        _generated.Clear();
        var builder = default(PdfContentBuilder);
        try
        {
            WriteAll(ref builder, mode);
            return builder.ToArray();
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Writes the content again.</summary>
    /// <param name="mode">Whether objects nobody changed keep their original bytes or are written from the model.</param>
    /// <param name="cancellationToken">Cancels the work before it starts.</param>
    /// <returns>The new content stream data, not compressed.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public ValueTask<byte[]> RegenerateAsync(PdfRegenerateMode mode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(Regenerate(mode));
    }

    /// <summary>Determines whether an object is written from the model rather than copied.</summary>
    /// <param name="item">The object.</param>
    /// <param name="mode">The mode.</param>
    /// <returns><see langword="true"/> when the object's bytes are written again.</returns>
    private static bool ShouldWrite(PdfPageObject item, PdfRegenerateMode mode)
    {
        if (item.IsModified)
        {
            return true;
        }

        return mode == PdfRegenerateMode.Rewrite && item is not PdfTextObject { IsOpaque: true } and not PdfImageObject { IsInline: true };
    }

    /// <summary>Writes a clipping path on its own, so a deleted or moved path still clips what follows.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="item">The object; only paths that clip write anything.</param>
    private static void WriteClipOnly(ref PdfContentBuilder builder, PdfPageObject item)
    {
        if (item is not PdfPathObject { Clip: not PdfClipMode.None } path)
        {
            return;
        }

        WriteSegments(ref builder, path.Segments);
        WriteClip(ref builder, path.Clip);
        builder.EndPath();
    }

    /// <summary>Writes a path: its segments, its clip when it keeps one, and its painting operator.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="path">The path.</param>
    /// <param name="withClip">Whether to write the clip too; when not, the caller writes it separately.</param>
    private static void WritePath(ref PdfContentBuilder builder, PdfPathObject path, bool withClip)
    {
        WriteSegments(ref builder, path.Segments);
        if (withClip)
        {
            WriteClip(ref builder, path.Clip);
        }

        switch (path.PaintMode)
        {
            case PdfPathPaintMode.Stroke:
            {
                WriteStroke(ref builder, path);
                break;
            }

            case PdfPathPaintMode.Fill:
            {
                WriteFill(ref builder, path);
                break;
            }

            case PdfPathPaintMode.FillStroke:
            {
                WriteFillStroke(ref builder, path);
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
    /// <param name="builder">The output.</param>
    /// <param name="path">The path.</param>
    private static void WriteStroke(ref PdfContentBuilder builder, PdfPathObject path)
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
    /// <param name="builder">The output.</param>
    /// <param name="path">The path.</param>
    private static void WriteFill(ref PdfContentBuilder builder, PdfPathObject path)
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
    /// <param name="builder">The output.</param>
    /// <param name="path">The path.</param>
    private static void WriteFillStroke(ref PdfContentBuilder builder, PdfPathObject path)
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
    /// <param name="builder">The output.</param>
    /// <param name="mode">The clip mode.</param>
    private static void WriteClip(ref PdfContentBuilder builder, PdfClipMode mode)
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
    /// <param name="builder">The output.</param>
    /// <param name="segments">The segments.</param>
    private static void WriteSegments(ref PdfContentBuilder builder, PdfPathSegment[] segments)
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

    /// <summary>Writes the <c>cm</c> that moves an object in user space.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="item">The object.</param>
    private static void WriteTransform(ref PdfContentBuilder builder, PdfPageObject item)
    {
        if (!item.IsTransformed || !TryGetLocalTransform(item, out var matrix))
        {
            return;
        }

        builder.Transform(matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32);
    }

    /// <summary>Gets the matrix a <c>cm</c> needs so the object moves by its transformation in user space.</summary>
    /// <param name="item">The object.</param>
    /// <param name="matrix">Receives the matrix.</param>
    /// <returns><see langword="false"/> when the object's own matrix cannot be inverted.</returns>
    private static bool TryGetLocalTransform(PdfPageObject item, out Matrix3x2 matrix)
    {
        if (!Matrix3x2.Invert(item.Matrix, out var inverse))
        {
            matrix = Matrix3x2.Identity;
            return false;
        }

        matrix = item.Matrix * item.Transformation * inverse;
        return true;
    }

    /// <summary>Sets a grey colour.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="gray">The level.</param>
    /// <param name="stroke">Whether it is the stroke colour.</param>
    private static void PaintGray(ref PdfContentBuilder builder, float gray, bool stroke)
    {
        if (stroke)
        {
            builder.SetStrokeGray(gray);
        }
        else
        {
            builder.SetFillGray(gray);
        }
    }

    /// <summary>Sets an RGB colour.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="c">The three components.</param>
    /// <param name="stroke">Whether it is the stroke colour.</param>
    private static void PaintRgb(ref PdfContentBuilder builder, float[] c, bool stroke)
    {
        if (stroke)
        {
            builder.SetStrokeRgb(c[0], c[1], c[RgbComponents - 1]);
        }
        else
        {
            builder.SetFillRgb(c[0], c[1], c[RgbComponents - 1]);
        }
    }

    /// <summary>Sets a CMYK colour.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="c">The four components.</param>
    /// <param name="stroke">Whether it is the stroke colour.</param>
    private static void PaintCmyk(ref PdfContentBuilder builder, float[] c, bool stroke)
    {
        if (stroke)
        {
            builder.SetStrokeCmyk(c[0], c[1], c[RgbComponents - 1], c[CmykComponents - 1]);
        }
        else
        {
            builder.SetFillCmyk(c[0], c[1], c[RgbComponents - 1], c[CmykComponents - 1]);
        }
    }

    /// <summary>Sets the components of a colour in a named colour space.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="components">The components.</param>
    /// <param name="stroke">Whether it is the stroke colour.</param>
    private static void PaintComponents(ref PdfContentBuilder builder, float[] components, bool stroke)
    {
        if (stroke)
        {
            builder.SetStrokeColorN(components);
        }
        else
        {
            builder.SetFillColorN(components);
        }
    }

    /// <summary>Writes the objects, the tail of the content and any appended content.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="mode">The mode.</param>
    private void WriteAll(ref PdfContentBuilder builder, PdfRegenerateMode mode)
    {
        var wrapped = _appended.Count > 0;
        for (var i = 0; wrapped && i <= _underflow; i++)
        {
            builder.SaveState();
        }

        var position = 0;
        var nextMark = 0;
        foreach (var item in _objects)
        {
            position = WriteScrubbedBegins(ref builder, item.Source.Start, position, ref nextMark);
            if (!ShouldWrite(item, mode) || item.Source.Start < position)
            {
                continue;
            }

            builder.WriteRaw(_source.AsSpan(position, item.Source.Start - position));
            WriteObject(ref builder, item, mode);
            position = item.Source.End;
        }

        position = WriteScrubbedBegins(ref builder, _source.Length, position, ref nextMark);
        builder.WriteRaw(_source.AsSpan(position));
        if (!wrapped)
        {
            return;
        }

        builder.WriteRaw(NewLine);
        for (var i = 0; i <= _unclosed; i++)
        {
            builder.RestoreState();
        }

        builder.WriteRaw(CollectionsMarshal.AsSpan(_appended));
    }

    /// <summary>Writes the opening operators of marked-content sequences whose text was scrubbed, for those that start before a limit.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="limit">The offset to stop at.</param>
    /// <param name="position">The offset the content has been written to.</param>
    /// <param name="next">The index of the next sequence to look at; advanced.</param>
    /// <returns>The offset the content has been written to now.</returns>
    private int WriteScrubbedBegins(ref PdfContentBuilder builder, int limit, int position, ref int next)
    {
        while (next < Begins.Count && Begins[next].BeginSource.End <= limit)
        {
            var mark = Begins[next];
            next++;
            if (!mark.IsScrubbed || mark.BeginSource.Start < position)
            {
                continue;
            }

            builder.WriteRaw(_source.AsSpan(position, mark.BeginSource.Start - position));
            WriteMarkBegin(ref builder, mark);
            position = mark.BeginSource.End;
        }

        return position;
    }

    /// <summary>Writes a marked-content opening without the text that redaction removed: no /ActualText and no /Alt.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="mark">The sequence.</param>
    private void WriteMarkBegin(ref PdfContentBuilder builder, PdfMark mark)
    {
        var names = Document.Objects.Names;
        builder.WriteRaw(NewLine);
        var tag = names.GetSpelling(mark.Tag);
        if (mark.Properties is not { } properties)
        {
            builder.BeginMarkedContent(tag);
            return;
        }

        var copy = properties.Clone();
        _ = copy.Remove(KnownName.ActualText);
        _ = copy.Remove(KnownName.Alt);
        if (mark.PropertiesName.IsNone)
        {
            builder.BeginMarkedContent(tag, copy, names);
            return;
        }

        var name = AllocateName(KnownName.Properties, "RdP");
        _generated.Add(new(KnownName.Properties, name, PdfValue.FromDictionary(copy), null));
        builder.BeginMarkedContent(tag, names.GetSpelling(name));
    }

    /// <summary>Writes one object.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="item">The object.</param>
    /// <param name="mode">The mode.</param>
    private void WriteObject(ref PdfContentBuilder builder, PdfPageObject item, PdfRegenerateMode mode)
    {
        builder.WriteRaw(NewLine);
        if (item is PdfTextObject text)
        {
            WriteText(ref builder, text);
            return;
        }

        if (item.IsDeleted)
        {
            WriteClipOnly(ref builder, item);
            return;
        }

        var wrap = item.IsTransformed || item.IsRecoloured;
        if (wrap)
        {
            builder.SaveState();
            WriteTransform(ref builder, item);
            WritePaints(ref builder, item);
        }

        WriteBody(ref builder, item, wrap, mode);
        if (!wrap)
        {
            return;
        }

        builder.RestoreState();
        WriteClipOnly(ref builder, item);
    }

    /// <summary>Writes the body of an object that is not text.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="item">The object.</param>
    /// <param name="wrapped">Whether a save and the object's changes were already written.</param>
    /// <param name="mode">The mode.</param>
    private void WriteBody(ref PdfContentBuilder builder, PdfPageObject item, bool wrapped, PdfRegenerateMode mode)
    {
        switch (item)
        {
            case PdfPathObject path:
            {
                WritePath(ref builder, path, !wrapped);
                break;
            }

            case PdfImageObject image:
            {
                WriteImage(ref builder, image, mode);
                break;
            }

            case PdfShadingObject shading:
            {
                builder.PaintShading(Document.Objects.Names.GetSpelling(shading.Name));
                break;
            }

            case PdfFormObject form:
            {
                WriteForm(ref builder, form);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Writes an image: the original bytes, or a <c>Do</c> of the replacement.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="image">The image.</param>
    /// <param name="mode">The mode.</param>
    private void WriteImage(ref PdfContentBuilder builder, PdfImageObject image, PdfRegenerateMode mode)
    {
        if (image.Replacement is { } replacement)
        {
            var name = AllocateName(KnownName.XObject, "Rg");
            _generated.Add(new(KnownName.XObject, name, PdfValue.FromStream(replacement), null));
            builder.DrawXObject(Document.Objects.Names.GetSpelling(name));
            return;
        }

        if (image.IsInline || mode == PdfRegenerateMode.Preserve)
        {
            WriteSource(ref builder, image);
            return;
        }

        builder.DrawXObject(Document.Objects.Names.GetSpelling(image.Name));
    }

    /// <summary>Writes a form: the original bytes, or a <c>Do</c> of a new form holding its changed content.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="form">The form.</param>
    private void WriteForm(ref PdfContentBuilder builder, PdfFormObject form)
    {
        if (form.ReadContent is { IsModified: true } changed)
        {
            var name = AllocateName(KnownName.XObject, "Rf");
            _generated.Add(new(KnownName.XObject, name, default, changed));
            builder.DrawXObject(Document.Objects.Names.GetSpelling(name));
            return;
        }

        builder.DrawXObject(Document.Objects.Names.GetSpelling(form.Name));
    }

    /// <summary>Copies an object's original bytes.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="item">The object.</param>
    private void WriteSource(ref PdfContentBuilder builder, PdfPageObject item)
    {
        builder.WriteRaw(_source.AsSpan(item.Source.Start, item.Source.Length));
        builder.WriteRaw(NewLine);
    }

    /// <summary>Writes the colour operators for an object's changed colours.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="item">The object.</param>
    private void WritePaints(ref PdfContentBuilder builder, PdfPageObject item)
    {
        if (item.ChangedFill is { } fill)
        {
            WritePaint(ref builder, fill, false);
        }

        if (item.ChangedStroke is { } stroke)
        {
            WritePaint(ref builder, stroke, true);
        }
    }

    /// <summary>Writes the colour operators that set a colour.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="paint">The colour.</param>
    /// <param name="stroke">Whether it is the stroke colour.</param>
    private void WritePaint(ref PdfContentBuilder builder, PdfPaint paint, bool stroke)
    {
        var components = paint.Components;
        switch (paint.ColorSpace.ToKnownName())
        {
            case KnownName.DeviceGray when components.Length == 1 && paint.Pattern.IsNone:
            {
                PaintGray(ref builder, components[0], stroke);
                break;
            }

            case KnownName.DeviceRGB when components.Length == RgbComponents && paint.Pattern.IsNone:
            {
                PaintRgb(ref builder, components, stroke);
                break;
            }

            case KnownName.DeviceCMYK when components.Length == CmykComponents && paint.Pattern.IsNone:
            {
                PaintCmyk(ref builder, components, stroke);
                break;
            }

            default:
            {
                PaintNamed(ref builder, paint, stroke);
                break;
            }
        }
    }

    /// <summary>Sets a colour in a named colour space, with an optional pattern.</summary>
    /// <param name="builder">The output.</param>
    /// <param name="paint">The colour.</param>
    /// <param name="stroke">Whether it is the stroke colour.</param>
    private void PaintNamed(ref PdfContentBuilder builder, PdfPaint paint, bool stroke)
    {
        var names = Document.Objects.Names;
        var space = names.GetSpelling(paint.ColorSpace);
        if (stroke)
        {
            builder.SetStrokeColorSpace(space);
        }
        else
        {
            builder.SetFillColorSpace(space);
        }

        if (paint.Pattern.IsNone)
        {
            if (paint.Components.Length > 0)
            {
                PaintComponents(ref builder, paint.Components, stroke);
            }

            return;
        }

        var pattern = names.GetSpelling(paint.Pattern);
        if (stroke)
        {
            builder.SetStrokeColorN(paint.Components, pattern);
        }
        else
        {
            builder.SetFillColorN(paint.Components, pattern);
        }
    }
}
