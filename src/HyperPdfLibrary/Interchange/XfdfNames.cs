// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;

namespace HyperPdfLibrary.Interchange;

/// <summary>The element and attribute names of XFDF (ISO 19444-1).</summary>
internal static class XfdfNames
{
    /// <summary>The XFDF namespace.</summary>
    internal const string Namespace = "http://ns.adobe.com/xfdf/";

    /// <summary>The root element.</summary>
    internal const string Root = "xfdf";

    /// <summary>The element naming the PDF file.</summary>
    internal const string File = "f";

    /// <summary>The element holding the file identifiers.</summary>
    internal const string Ids = "ids";

    /// <summary>The element holding the form fields.</summary>
    internal const string Fields = "fields";

    /// <summary>The element of one form field.</summary>
    internal const string Field = "field";

    /// <summary>The element holding the annotations.</summary>
    internal const string Annots = "annots";

    /// <summary>The element of a field value.</summary>
    internal const string Value = "value";

    /// <summary>The element of a rich text field value.</summary>
    internal const string ValueRichText = "value-richtext";

    /// <summary>The element of an annotation's text.</summary>
    internal const string Contents = "contents";

    /// <summary>The element of an annotation's rich text.</summary>
    internal const string ContentsRichText = "contents-richtext";

    /// <summary>The element of a default appearance string.</summary>
    internal const string DefaultAppearance = "defaultappearance";

    /// <summary>The element of a default style string.</summary>
    internal const string DefaultStyle = "defaultstyle";

    /// <summary>The element of a pop-up window.</summary>
    internal const string Popup = "popup";

    /// <summary>The element holding the strokes of an ink annotation.</summary>
    internal const string InkList = "inklist";

    /// <summary>The element of one stroke.</summary>
    internal const string Gesture = "gesture";

    /// <summary>The element holding the points of a polygon or polyline.</summary>
    internal const string Vertices = "vertices";

    /// <summary>The element holding an attached file's bytes.</summary>
    internal const string Data = "data";

    /// <summary>The XHTML namespace rich text uses.</summary>
    internal const string Xhtml = "http://www.w3.org/1999/xhtml";

    /// <summary>The annotation element of each PDF subtype.</summary>
    private static readonly FrozenDictionary<string, string> ElementBySubtype = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Text"] = "text",
        ["Highlight"] = "highlight",
        ["Underline"] = "underline",
        ["StrikeOut"] = "strikeout",
        ["Squiggly"] = "squiggly",
        ["Ink"] = "ink",
        ["Square"] = "square",
        ["Circle"] = "circle",
        ["Line"] = "line",
        ["Polygon"] = "polygon",
        ["PolyLine"] = "polyline",
        ["FreeText"] = "freetext",
        ["Stamp"] = "stamp",
        ["Caret"] = "caret",
        ["FileAttachment"] = "fileattachment",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The PDF subtype of each annotation element.</summary>
    private static readonly FrozenDictionary<string, string> SubtypeByElement = CreateReverse();

    /// <summary>Finds the PDF subtype an annotation element stands for.</summary>
    /// <param name="element">The element's local name.</param>
    /// <param name="subtype">The PDF subtype.</param>
    /// <returns><see langword="true"/> when the element is an annotation this library carries.</returns>
    internal static bool TryGetSubtype(string element, out string subtype)
    {
        if (SubtypeByElement.TryGetValue(element, out var found))
        {
            subtype = found;
            return true;
        }

        subtype = string.Empty;
        return false;
    }

    /// <summary>Finds the element for a PDF subtype.</summary>
    /// <param name="subtype">The PDF subtype.</param>
    /// <param name="element">The element's name.</param>
    /// <returns><see langword="true"/> when the subtype has an element.</returns>
    internal static bool TryGetElement(string subtype, out string element)
    {
        if (ElementBySubtype.TryGetValue(subtype, out var found))
        {
            element = found;
            return true;
        }

        element = string.Empty;
        return false;
    }

    /// <summary>Builds the lookup from element to subtype, ignoring case.</summary>
    /// <returns>The lookup.</returns>
    private static FrozenDictionary<string, string> CreateReverse()
    {
        var reverse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (subtype, element) in ElementBySubtype)
        {
            reverse[element] = subtype;
        }

        return reverse.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
