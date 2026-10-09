// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <summary>Reads an annotation dictionary into a <see cref="PdfInterchangeAnnotation"/>.</summary>
internal static class InterchangeAnnotationReader
{
    /// <summary>The numbers in a line's end points.</summary>
    private const int LineNumbers = 4;

    /// <summary>The line endings a line has: start and end.</summary>
    private const int LineEndings = 2;

    /// <summary>The entries of a complete <c>/Border</c> array.</summary>
    private const int BorderEntries = 3;

    /// <summary>Determines whether a subtype is one the interchange formats carry.</summary>
    /// <param name="subtype">The PDF subtype name.</param>
    /// <returns><see langword="true"/> when supported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsSupported(string subtype) => XfdfNames.TryGetElement(subtype, out _);

    /// <summary>Reads an annotation.</summary>
    /// <param name="annotation">The annotation dictionary.</param>
    /// <param name="id">The annotation's object id, or an invalid id when it is inline.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="names">The names given to annotations without one, by object number.</param>
    /// <returns>The annotation; <see langword="null"/> when its subtype is not carried.</returns>
    internal static PdfInterchangeAnnotation? Read(PdfDictionary annotation, PdfObjectId id, int pageIndex, Dictionary<int, string> names)
    {
        var table = annotation.Owner?.Names;
        var subtype = table is null ? string.Empty : table.GetString(annotation.GetName(KnownName.Subtype));
        if (table is null || !IsSupported(subtype))
        {
            return null;
        }

        var result = new PdfInterchangeAnnotation(subtype) { Page = pageIndex };
        ReadCommon(annotation, result, id, names);
        ReadBorder(annotation, result);
        ReadReply(annotation, result, names);
        ReadGeometry(annotation, result);
        ReadText(annotation, result, table);
        result.Popup = ReadPopup(annotation, pageIndex);
        result.Attachment = subtype == "FileAttachment" ? ReadAttachment(annotation) : null;
        return result;
    }

    /// <summary>Reads the entries every annotation has.</summary>
    /// <param name="annotation">The dictionary.</param>
    /// <param name="result">The result.</param>
    /// <param name="id">The object id.</param>
    /// <param name="names">The generated names.</param>
    private static void ReadCommon(PdfDictionary annotation, PdfInterchangeAnnotation result, PdfObjectId id, Dictionary<int, string> names)
    {
        result.Rect = annotation.TryGetRectangle(KnownName.Rect, out var rectangle) ? rectangle : null;
        result.Color = PdfAnnotations.TryGetColor(annotation, KnownName.C, out var color) ? color : null;
        result.InteriorColor = PdfAnnotations.TryGetColor(annotation, KnownName.IC, out var interior) ? interior : null;
        result.Flags = PdfAnnotations.GetFlags(annotation);
        result.Name = annotation.GetText(KnownName.NM) ?? (id.IsValid && names.TryGetValue(id.Number, out var generated) ? generated : null);
        result.Title = annotation.GetText(KnownName.T);
        result.Date = PdfAnnotations.GetDate(annotation, KnownName.M);
        result.CreationDate = PdfAnnotations.GetDate(annotation, KnownName.CreationDate);
        result.Opacity = annotation.ContainsKey(KnownName.CA) ? annotation.GetSingle(KnownName.CA) : null;
        result.Justification = annotation.ContainsKey(KnownName.Q) ? annotation.GetInt32(KnownName.Q) : null;
    }

    /// <summary>Reads the border width, style, dashes and cloud intensity.</summary>
    /// <param name="annotation">The dictionary.</param>
    /// <param name="result">The result.</param>
    private static void ReadBorder(PdfDictionary annotation, PdfInterchangeAnnotation result)
    {
        if (annotation.GetDictionary(KnownName.BS) is { } style)
        {
            result.Width = style.ContainsKey(KnownName.W) ? style.GetSingle(KnownName.W) : null;
            result.Style = style.GetName(KnownName.S) is { IsNone: false } letter ? annotation.Owner!.Names.GetString(letter) : null;
            result.Dashes = style.GetArray(KnownName.D) is { } dashes ? ReadNumbers(dashes) : default;
        }
        else if (annotation.GetArray(KnownName.Border) is { Count: >= BorderEntries })
        {
            result.Width = PdfAnnotations.GetBorderWidth(annotation);
        }

        result.Intensity = annotation.GetDictionary(KnownName.BE) is { } effect && effect.IsName(KnownName.S, KnownName.C)
            ? effect.GetSingle(KnownName.I)
            : null;
    }

    /// <summary>Reads the reply and review entries.</summary>
    /// <param name="annotation">The dictionary.</param>
    /// <param name="result">The result.</param>
    /// <param name="names">The generated names.</param>
    private static void ReadReply(PdfDictionary annotation, PdfInterchangeAnnotation result, Dictionary<int, string> names)
    {
        if (annotation.GetDictionary(KnownName.IRT) is { } parent)
        {
            var number = annotation.GetRaw(KnownName.IRT).AsReference().Number;
            result.InReplyTo = parent.GetText(KnownName.NM) ?? (names.TryGetValue(number, out var generated) ? generated : null);
            result.ReplyType = annotation.GetName(KnownName.RT).IsNone ? null : annotation.Owner!.Names.GetString(annotation.GetName(KnownName.RT));
        }

        result.State = annotation.GetText(KnownName.State);
        result.StateModel = annotation.GetText(KnownName.StateModel);
    }

    /// <summary>Reads the shape entries of the annotation's subtype.</summary>
    /// <param name="annotation">The dictionary.</param>
    /// <param name="result">The result.</param>
    private static void ReadGeometry(PdfDictionary annotation, PdfInterchangeAnnotation result)
    {
        result.Coords = ReadOptionalNumbers(annotation, KnownName.QuadPoints);
        result.Vertices = ReadOptionalNumbers(annotation, KnownName.Vertices);
        result.Callout = ReadOptionalNumbers(annotation, KnownName.CL);
        result.Fringe = ReadOptionalNumbers(annotation, KnownName.RD);
        var line = ReadOptionalNumbers(annotation, KnownName.L);
        result.Line = line.Length >= LineNumbers ? line : default;
        result.Gestures = ReadGestures(annotation);
        if (annotation.GetArray(KnownName.LE) is not { Count: >= LineEndings } endings)
        {
            return;
        }

        var table = annotation.Owner!.Names;
        result.Head = table.GetString(endings.GetName(0));
        result.Tail = table.GetString(endings.GetName(1));
    }

    /// <summary>Reads the text entries, icon and state of the annotation.</summary>
    /// <param name="annotation">The dictionary.</param>
    /// <param name="result">The result.</param>
    /// <param name="table">The document's names.</param>
    private static void ReadText(PdfDictionary annotation, PdfInterchangeAnnotation result, PdfNameTable table)
    {
        result.Subject = annotation.GetText(table.Intern("Subj"));
        result.Contents = annotation.GetText(KnownName.Contents);
        result.RichContents = ReadLongText(annotation.Get(KnownName.RC));
        result.DefaultAppearance = annotation.GetText(KnownName.DA);
        result.DefaultStyle = ReadLongText(annotation.Get(KnownName.DS));
        result.Icon = annotation.GetName(KnownName.Name) is { IsNone: false } icon ? table.GetString(icon) : null;
        result.Symbol = annotation.GetName(table.Intern("Sy")) is { IsNone: false } symbol ? table.GetString(symbol) : null;
        result.IsOpen = annotation.Get(KnownName.Open) is { Kind: PdfKind.Boolean } open ? open.AsBoolean() : null;
    }

    /// <summary>Reads a text entry that may be a string or a stream.</summary>
    /// <param name="value">The resolved value.</param>
    /// <returns>The text, or <see langword="null"/> when there is none.</returns>
    private static string? ReadLongText(PdfValue value) =>
        value.Kind is PdfKind.String or PdfKind.Stream ? FieldAttributes.ReadText(value) : null;

    /// <summary>Reads a pop-up window.</summary>
    /// <param name="annotation">The parent annotation.</param>
    /// <param name="pageIndex">The parent's page.</param>
    /// <returns>The pop-up, or <see langword="null"/>.</returns>
    private static PdfInterchangePopup? ReadPopup(PdfDictionary annotation, int pageIndex) =>
        annotation.GetDictionary(KnownName.Popup) is not { } popup || !popup.TryGetRectangle(KnownName.Rect, out var rectangle)
            ? null
            : new(pageIndex, rectangle, popup.GetBoolean(KnownName.Open));

    /// <summary>Reads the file a file attachment annotation carries.</summary>
    /// <param name="annotation">The dictionary.</param>
    /// <returns>The file, or <see langword="null"/> when the annotation names a file without carrying it.</returns>
    private static PdfInterchangeAttachment? ReadAttachment(PdfDictionary annotation)
    {
        if (annotation.GetDictionary(annotation.Owner!.Names.Intern("FS")) is not { } spec
            || spec.GetDictionary(KnownName.EF)?.GetStream(KnownName.F) is not { } stream)
        {
            return null;
        }

        var name = spec.GetText(KnownName.UF) ?? spec.GetText(KnownName.F) ?? string.Empty;
        var type = stream.Dictionary.GetName(KnownName.Subtype);
        var mime = type.IsNone ? null : annotation.Owner!.Names.GetString(type);
        return new(name, stream.DecodeToArray(), spec.GetText(KnownName.Desc), mime);
    }

    /// <summary>Reads the strokes of an ink annotation.</summary>
    /// <param name="annotation">The dictionary.</param>
    /// <returns>The strokes; empty when there are none.</returns>
    private static ReadOnlyMemory<ReadOnlyMemory<float>> ReadGestures(PdfDictionary annotation)
    {
        if (annotation.GetArray(KnownName.InkList) is not { } list)
        {
            return default;
        }

        var strokes = new ReadOnlyMemory<float>[list.Count];
        for (var i = 0; i < strokes.Length; i++)
        {
            strokes[i] = list.GetArray(i) is { } stroke ? ReadNumbers(stroke) : default;
        }

        return strokes;
    }

    /// <summary>Reads an array of numbers stored under a key.</summary>
    /// <param name="annotation">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The numbers; empty when the entry is missing or not an array.</returns>
    private static float[] ReadOptionalNumbers(PdfDictionary annotation, PdfName key) =>
        annotation.GetArray(key) is { } array ? ReadNumbers(array) : [];

    /// <summary>Reads every item of an array as a number.</summary>
    /// <param name="array">The array.</param>
    /// <returns>The numbers.</returns>
    private static float[] ReadNumbers(PdfArray array)
    {
        var values = new float[array.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = array.GetSingle(i);
        }

        return values;
    }
}
