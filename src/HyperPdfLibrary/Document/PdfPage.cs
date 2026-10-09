// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>
/// A page's dictionary and the attributes it inherits from the page tree. Viewer space is the rotated crop box in points
/// with the origin at the top left, matching what viewers lay out.
/// </summary>
[DebuggerDisplay("PdfPage: {Index} {Width}x{Height} rotate {Rotation}")]
public sealed class PdfPage
{
    /// <summary>A quarter turn in degrees.</summary>
    private const int QuarterTurn = 90;

    /// <summary>A half turn in degrees.</summary>
    private const int HalfTurn = 180;

    /// <summary>Three quarter turns in degrees.</summary>
    private const int ThreeQuarterTurn = 270;

    /// <summary>The quarter turns in a full turn.</summary>
    private const int QuarterTurnsPerCircle = 4;

    /// <summary>
    /// The largest coordinate magnitude and extent a page box may have, in points. PDFium has no limit, so this only
    /// stops values that would overflow later arithmetic.
    /// </summary>
    private const float MaxBoxExtent = 1_000_000F;

    /// <summary>The width of a US Letter page in points.</summary>
    private const float LetterWidth = 612;

    /// <summary>The height of a US Letter page in points.</summary>
    private const float LetterHeight = 792;

    /// <summary>Initializes a new instance of the <see cref="PdfPage"/> class.</summary>
    /// <param name="index">The zero based page index.</param>
    /// <param name="id">The page object's id, or an invalid id for a direct page dictionary.</param>
    /// <param name="dictionary">The page dictionary.</param>
    /// <param name="inherited">The attributes inherited from the page tree.</param>
    internal PdfPage(int index, PdfObjectId id, PdfDictionary dictionary, in InheritedAttributes inherited)
    {
        Index = index;
        Id = id;
        Dictionary = dictionary;
        PageBoxes.Report(dictionary, id.Number, dictionary.Owner?.Context);
        Resources = dictionary.GetDictionary(KnownName.Resources) ?? inherited.Resources;
        MediaBox = ReadBox(dictionary, KnownName.MediaBox) ?? inherited.MediaBox ?? DefaultMediaBox;
        var crop = ReadBox(dictionary, KnownName.CropBox) ?? inherited.CropBox;
        CropBox = ClipCropBox(crop, MediaBox);
        var rotate = dictionary.Get(KnownName.Rotate);
        Rotation = NormaliseRotation(rotate.IsNumber ? rotate.AsInt32() : inherited.Rotate);
        var upright = Rotation % HalfTurn == 0;
        Width = upright ? CropBox.Width : CropBox.Height;
        Height = upright ? CropBox.Height : CropBox.Width;
        ViewerTransform = CreateViewerTransform(CropBox, Rotation);
        _ = Matrix3x2.Invert(ViewerTransform, out var inverse);
        UserTransform = inverse;
    }

    /// <summary>Gets the US Letter media box used when a page has none.</summary>
    public static PdfRectangle DefaultMediaBox => new(0, 0, LetterWidth, LetterHeight);

    /// <summary>Gets the zero based page index.</summary>
    public int Index { get; }

    /// <summary>Gets the page object's id.</summary>
    public PdfObjectId Id { get; }

    /// <summary>Gets the page dictionary.</summary>
    public PdfDictionary Dictionary { get; }

    /// <summary>Gets the page's resources, inherited when the page has none of its own.</summary>
    public PdfDictionary? Resources { get; }

    /// <summary>Gets the media box.</summary>
    public PdfRectangle MediaBox { get; }

    /// <summary>Gets the crop box, clipped to the media box.</summary>
    public PdfRectangle CropBox { get; }

    /// <summary>Gets the clockwise display rotation: 0, 90, 180 or 270.</summary>
    public int Rotation { get; }

    /// <summary>Gets the displayed width in points, after rotation.</summary>
    public float Width { get; }

    /// <summary>Gets the displayed height in points, after rotation.</summary>
    public float Height { get; }

    /// <summary>Gets the transform from user space to viewer space (top-left origin, rotation applied).</summary>
    public Matrix3x2 ViewerTransform { get; }

    /// <summary>Gets the transform from viewer space back to user space.</summary>
    public Matrix3x2 UserTransform { get; }

    /// <summary>Converts a user space point to viewer space.</summary>
    /// <param name="point">The point in user space.</param>
    /// <returns>The point in viewer space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 ToViewer(Vector2 point) => Vector2.Transform(point, ViewerTransform);

    /// <summary>Converts a viewer space point to user space.</summary>
    /// <param name="point">The point in viewer space.</param>
    /// <returns>The point in user space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 ToUser(Vector2 point) => Vector2.Transform(point, UserTransform);

    /// <summary>Converts a user space rectangle to viewer space.</summary>
    /// <param name="rectangle">The rectangle in user space.</param>
    /// <returns>The bounding rectangle in viewer space, as left, top, right, bottom.</returns>
    public PdfRectangle ToViewerRectangle(PdfRectangle rectangle)
    {
        var a = ToViewer(new(rectangle.Left, rectangle.Bottom));
        var b = ToViewer(new(rectangle.Right, rectangle.Top));
        return PdfRectangle.FromCorners(a.X, a.Y, b.X, b.Y);
    }

    /// <summary>Normalises a rotation to 0, 90, 180 or 270; values that are not quarter turns are rounded down.</summary>
    /// <param name="rotate">The /Rotate value.</param>
    /// <returns>The rotation.</returns>
    internal static int NormaliseRotation(int rotate)
    {
        // Truncates toward zero like PDFium: 100 becomes 90 and -100 becomes 270.
        var turns = ((rotate / QuarterTurn % QuarterTurnsPerCircle) + QuarterTurnsPerCircle) % QuarterTurnsPerCircle;
        return turns * QuarterTurn;
    }

    /// <summary>Reads a box, ignoring malformed, empty, non-finite or absurdly large ones.</summary>
    /// <param name="dictionary">The page dictionary.</param>
    /// <param name="key">The box key.</param>
    /// <returns>The box, or <see langword="null"/>.</returns>
    internal static PdfRectangle? ReadBox(PdfDictionary dictionary, KnownName key) =>
        dictionary.TryGetRectangle(key, out var box) && IsUsableBox(box) ? box : null;

    /// <summary>Determines whether a box is finite, has an area and stays within <see cref="MaxBoxExtent"/>.</summary>
    /// <param name="box">The box.</param>
    /// <returns><see langword="true"/> when usable.</returns>
    internal static bool IsUsableBox(in PdfRectangle box) =>
        !box.IsEmpty
        && float.IsFinite(box.Width)
        && float.IsFinite(box.Height)
        && box.Width < MaxBoxExtent
        && box.Height < MaxBoxExtent
        && MathF.Abs(box.Left) < MaxBoxExtent
        && MathF.Abs(box.Bottom) < MaxBoxExtent
        && MathF.Abs(box.Right) < MaxBoxExtent
        && MathF.Abs(box.Top) < MaxBoxExtent;

    /// <summary>Clips the crop box to the media box, as viewers do; a crop box outside the media box is ignored.</summary>
    /// <param name="crop">The crop box, if any.</param>
    /// <param name="media">The media box.</param>
    /// <returns>The effective crop box.</returns>
    private static PdfRectangle ClipCropBox(PdfRectangle? crop, PdfRectangle media)
    {
        if (crop is not { } box)
        {
            return media;
        }

        var clipped = box.Intersect(media);
        return clipped.IsEmpty ? media : clipped;
    }

    /// <summary>Creates the user-to-viewer transform for a crop box and rotation.</summary>
    /// <param name="crop">The crop box.</param>
    /// <param name="rotation">The rotation.</param>
    /// <returns>The transform.</returns>
    private static Matrix3x2 CreateViewerTransform(PdfRectangle crop, int rotation) => rotation switch
    {
        QuarterTurn => new(0, 1, 1, 0, -crop.Bottom, -crop.Left),
        HalfTurn => new(-1, 0, 0, 1, crop.Right, -crop.Bottom),
        ThreeQuarterTurn => new(0, -1, -1, 0, crop.Top, crop.Right),
        _ => new(1, 0, 0, -1, -crop.Left, crop.Top),
    };
}
