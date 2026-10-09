// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.PageObjects;

/// <summary>
/// One object a content stream paints, with the graphics state it is painted in. Objects can be deleted, moved and
/// recoloured; the owning <see cref="PdfPageContent"/> writes the changes when it regenerates the content.
/// </summary>
[DebuggerDisplay("PdfPageObject: {Kind} at {Bounds}")]
public abstract class PdfPageObject
{
    /// <summary>The marked-content sequences of an object outside any.</summary>
    private static readonly PdfMark[] NoMarks = [];

    /// <summary>The paint set by <see cref="SetFillPaint"/>, or null while the content's own fill applies.</summary>
    private PdfPaint? _fill;

    /// <summary>The paint set by <see cref="SetStrokePaint"/>, or null while the content's own stroke applies.</summary>
    private PdfPaint? _stroke;

    /// <summary>The clipping paths, made on first use.</summary>
    private PdfClipPath[]? _clipPaths;

    /// <summary>Initializes a new instance of the <see cref="PdfPageObject"/> class.</summary>
    private protected PdfPageObject()
    {
    }

    /// <summary>Gets the kind of object.</summary>
    public abstract PdfPageObjectKind Kind { get; }

    /// <summary>Gets the object's position among the objects of its content, in painting order.</summary>
    public int Index { get; internal set; }

    /// <summary>Gets the matrix from the object's own coordinates to user space, as the content stream set it.</summary>
    public Matrix3x2 Matrix { get; internal set; } = Matrix3x2.Identity;

    /// <summary>Gets the transformation added by <see cref="Transform"/>, in user space; the identity until then.</summary>
    public Matrix3x2 Transformation { get; private set; } = Matrix3x2.Identity;

    /// <summary>Gets the object's bounds in user space, moved by <see cref="Transformation"/>; strokes include their line width.</summary>
    public PdfRectangle Bounds => IsTransformed && PageGeometry.IsFinite(OriginalBounds) ? TextGeometry.TransformRect(Transformation, OriginalBounds) : OriginalBounds;

    /// <summary>Gets the object's bounds in user space as the content stream paints it.</summary>
    public PdfRectangle OriginalBounds { get; internal set; }

    /// <summary>Gets the intersection of the bounds of the clipping paths in force, or an unbounded rectangle when none is.</summary>
    public PdfRectangle ClipBounds { get; internal set; } = Unbounded;

    /// <summary>Gets a value indicating whether a clipping path was in force when the object was painted.</summary>
    public bool IsClipped => ClipChain is not null;

    /// <summary>Gets the clipping paths in force, outermost first.</summary>
    public IReadOnlyList<PdfClipPath> ClipPaths => _clipPaths ??= ClipNode.ToArray(ClipChain);

    /// <summary>Gets the marked-content sequences the object sits inside, outermost first.</summary>
    public IReadOnlyList<PdfMark> Marks { get; internal set; } = NoMarks;

    /// <summary>Gets the line width in user space units at the time the object was painted.</summary>
    public float LineWidth { get; internal set; }

    /// <summary>Gets the colour used for filling, or for stencil images and text.</summary>
    public PdfPaint FillPaint => _fill ?? OriginalFillPaint;

    /// <summary>Gets the colour used for stroking.</summary>
    public PdfPaint StrokePaint => _stroke ?? OriginalStrokePaint;

    /// <summary>Gets a value indicating whether the object is deleted from the regenerated content.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="Transform"/> moved the object.</summary>
    public bool IsTransformed { get; private set; }

    /// <summary>Gets a value indicating whether the object's fill or stroke colour was changed.</summary>
    public bool IsRecoloured => _fill is not null || _stroke is not null;

    /// <summary>Gets a value indicating whether the object has any change to write.</summary>
    public virtual bool IsModified => IsDeleted || IsTransformed || IsRecoloured;

    /// <summary>Gets the unbounded rectangle used for objects with no clip.</summary>
    internal static PdfRectangle Unbounded { get; } = new(float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity);

    /// <summary>Gets or sets the content that owns the object.</summary>
    internal PdfPageContent? Owner { get; set; }

    /// <summary>Gets or sets the bytes of the content stream the object came from, with the operators that paint it.</summary>
    internal ByteRange Source { get; set; }

    /// <summary>Gets or sets the innermost clip in force.</summary>
    internal ClipNode? ClipChain { get; set; }

    /// <summary>Gets or sets the fill colour the content stream set.</summary>
    internal PdfPaint OriginalFillPaint { get; set; } = PdfPaint.Black;

    /// <summary>Gets or sets the stroke colour the content stream set.</summary>
    internal PdfPaint OriginalStrokePaint { get; set; } = PdfPaint.Black;

    /// <summary>Gets the colour to write for filling, or null to leave the content's own.</summary>
    internal PdfPaint? ChangedFill => _fill;

    /// <summary>Gets the colour to write for stroking, or null to leave the content's own.</summary>
    internal PdfPaint? ChangedStroke => _stroke;

    /// <summary>Deletes the object. The regenerated content leaves it out and keeps everything else in place.</summary>
    public void Delete() => IsDeleted = true;

    /// <summary>Brings back a deleted object.</summary>
    public void Undelete() => IsDeleted = false;

    /// <summary>Moves, scales or rotates the object. The change adds to earlier calls.</summary>
    /// <param name="matrix">The transformation in user space.</param>
    public void Transform(Matrix3x2 matrix)
    {
        Transformation = IsTransformed ? Transformation * matrix : matrix;
        IsTransformed = Transformation != Matrix3x2.Identity;
    }

    /// <summary>Moves the object.</summary>
    /// <param name="dx">The distance along x in user space units.</param>
    /// <param name="dy">The distance along y in user space units.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Translate(float dx, float dy) => Transform(Matrix3x2.CreateTranslation(dx, dy));

    /// <summary>Changes the fill colour.</summary>
    /// <param name="paint">The new colour.</param>
    /// <exception cref="ArgumentNullException"><paramref name="paint"/> is <see langword="null"/>.</exception>
    public void SetFillPaint(PdfPaint paint)
    {
        ArgumentNullException.ThrowIfNull(paint);
        _fill = ReferenceEquals(paint, OriginalFillPaint) ? null : paint;
    }

    /// <summary>Changes the stroke colour.</summary>
    /// <param name="paint">The new colour.</param>
    /// <exception cref="ArgumentNullException"><paramref name="paint"/> is <see langword="null"/>.</exception>
    public void SetStrokePaint(PdfPaint paint)
    {
        ArgumentNullException.ThrowIfNull(paint);
        _stroke = ReferenceEquals(paint, OriginalStrokePaint) ? null : paint;
    }
}
