// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Globalization;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <summary>Reads the attributes of an XFDF annotation element into a <see cref="PdfInterchangeAnnotation"/>.</summary>
internal static class XfdfAttributes
{
    /// <summary>The numbers in a point.</summary>
    private const int PointNumbers = 2;

    /// <summary>The numbers in a line's end points.</summary>
    private const int LineNumbers = 4;

    /// <summary>The setter for each attribute, by name.</summary>
    private static readonly FrozenDictionary<string, Action<PdfInterchangeAnnotation, string>> Setters = CreateSetters();

    /// <summary>Applies an attribute.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="name">The attribute's local name.</param>
    /// <param name="value">The attribute's value.</param>
    /// <returns><see langword="true"/> when the attribute is one this library reads.</returns>
    internal static bool Apply(PdfInterchangeAnnotation annotation, string name, string value)
    {
        if (!Setters.TryGetValue(name, out var setter))
        {
            return false;
        }

        setter(annotation, value);
        return true;
    }

    /// <summary>Reads a whole number.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The number, or <see langword="null"/> when unreadable.</returns>
    internal static int? ParseInt(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>Reads a number.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The number, or <see langword="null"/> when unreadable.</returns>
    internal static float? ParseFloat(string text) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>Builds the setters.</summary>
    /// <returns>The lookup.</returns>
    private static FrozenDictionary<string, Action<PdfInterchangeAnnotation, string>> CreateSetters()
    {
        var setters = new Dictionary<string, Action<PdfInterchangeAnnotation, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["page"] = static (a, v) => a.Page = ParseInt(v) ?? 0,
            ["rect"] = static (a, v) => a.Rect = InterchangeValues.TryParseRect(v, out var rectangle) ? rectangle : null,
            ["color"] = static (a, v) => a.Color = InterchangeValues.TryParseColor(v, out var color) ? color : null,
            ["interior-color"] = static (a, v) => a.InteriorColor = InterchangeValues.TryParseColor(v, out var color) ? color : null,
            ["flags"] = static (a, v) => a.Flags = InterchangeValues.ParseFlags(v),
            ["name"] = static (a, v) => a.Name = v,
            ["title"] = static (a, v) => a.Title = v,
            ["subject"] = static (a, v) => a.Subject = v,
            ["date"] = static (a, v) => a.Date = InterchangeValues.ParseDate(v),
            ["creationdate"] = static (a, v) => a.CreationDate = InterchangeValues.ParseDate(v),
            ["opacity"] = static (a, v) => a.Opacity = ParseFloat(v),
            ["width"] = static (a, v) => a.Width = ParseFloat(v),
            ["style"] = static (a, v) => a.Style = InterchangeValues.ParseStyle(v),
            ["dashes"] = static (a, v) => a.Dashes = InterchangeValues.ParseList(v),
            ["intensity"] = static (a, v) => a.Intensity = ParseFloat(v),
            ["inreplyto"] = static (a, v) => a.InReplyTo = v,
            ["replytype"] = static (a, v) => a.ReplyType = v,
            ["state"] = static (a, v) => a.State = v,
            ["statemodel"] = static (a, v) => a.StateModel = v,
            ["icon"] = static (a, v) => a.Icon = v,
            ["symbol"] = static (a, v) => a.Symbol = v,
            ["open"] = static (a, v) => a.IsOpen = InterchangeValues.ParseBoolean(v),
            ["coords"] = static (a, v) => a.Coords = InterchangeValues.ParseList(v),
            ["start"] = static (a, v) => SetLineEnd(a, v, 0),
            ["end"] = static (a, v) => SetLineEnd(a, v, PointNumbers),
            ["head"] = static (a, v) => a.Head = v,
            ["tail"] = static (a, v) => a.Tail = v,
            ["justification"] = static (a, v) => a.Justification = ParseInt(v),
            ["callout"] = static (a, v) => a.Callout = InterchangeValues.ParseList(v),
            ["fringe"] = static (a, v) => a.Fringe = InterchangeValues.ParseList(v),
        };
        return setters.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Sets one end of a line from <c>x,y</c>.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="text">The point.</param>
    /// <param name="offset">0 for the start, 2 for the end.</param>
    private static void SetLineEnd(PdfInterchangeAnnotation annotation, string text, int offset)
    {
        var point = InterchangeValues.ParseList(text);
        if (point.Length < PointNumbers)
        {
            return;
        }

        var line = new float[LineNumbers];
        annotation.Line.Span.CopyTo(line);
        line[offset] = point[0];
        line[offset + 1] = point[1];
        annotation.Line = line;
    }
}
