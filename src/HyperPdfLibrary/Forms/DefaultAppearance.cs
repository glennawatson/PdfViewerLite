// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>A field's default appearance string (<c>/DA</c>): the font, its size and the text colour.</summary>
/// <param name="FontName">The font's name in the resources, without the slash; empty when the string names none.</param>
/// <param name="FontSize">The size; 0 means the text is fitted to the field.</param>
/// <param name="Color">The colour operators, such as "0 g".</param>
[DebuggerDisplay("DefaultAppearance: {FontName} {FontSize} {Color}")]
internal sealed record DefaultAppearance(string FontName, float FontSize, string Color)
{
    /// <summary>The colour used when the string sets none.</summary>
    private const string BlackText = "0 g";

    /// <summary>The operand count of the gray operator.</summary>
    private const int GrayOperands = 1;

    /// <summary>The operand count of the RGB operator.</summary>
    private const int RgbOperands = 3;

    /// <summary>The operand count of the CMYK operator.</summary>
    private const int CmykOperands = 4;

    /// <summary>The operand count of the font operator.</summary>
    private const int FontOperands = 2;

    /// <summary>The number of hex digits in a name escape.</summary>
    private const int HexLength = 2;

    /// <summary>Gets the appearance used when a field has none.</summary>
    internal static DefaultAppearance Empty { get; } = new(string.Empty, 0, BlackText);

    /// <summary>Gets the colour operators for stroking, such as "0 G".</summary>
    internal string StrokeColor
    {
        get
        {
            var split = Color.LastIndexOf(' ') + 1;
            return string.Concat(Color.AsSpan(0, split), Color[split..].ToUpperInvariant());
        }
    }

    /// <summary>Finds the default appearance of a widget: its own, its field's, then the form's.</summary>
    /// <param name="widget">The widget dictionary.</param>
    /// <param name="field">The field dictionary.</param>
    /// <param name="form">The AcroForm dictionary, or <see langword="null"/>.</param>
    /// <returns>The parsed appearance.</returns>
    internal static DefaultAppearance Find(PdfDictionary widget, PdfDictionary field, PdfDictionary? form)
    {
        if (widget.ContainsKey(KnownName.DA))
        {
            return Parse(Latin1(widget.Get(KnownName.DA)));
        }

        var inherited = FieldAttributes.Find(field, KnownName.DA);
        if (!inherited.IsNull)
        {
            return Parse(Latin1(inherited));
        }

        return form is null ? Empty : Parse(Latin1(form.Get(KnownName.DA)));
    }

    /// <summary>Parses a default appearance string.</summary>
    /// <param name="text">The string.</param>
    /// <returns>The parsed appearance.</returns>
    internal static DefaultAppearance Parse(string text)
    {
        var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var name = string.Empty;
        var size = 0F;
        var color = BlackText;
        for (var i = 0; i < tokens.Length; i++)
        {
            switch (tokens[i])
            {
                case "Tf" when i >= FontOperands:
                {
                    name = DecodeName(tokens[i - FontOperands]);
                    size = ParseNumber(tokens[i - 1]);
                    break;
                }

                case "g":
                {
                    color = Operator(tokens, i, GrayOperands);
                    break;
                }

                case "rg":
                {
                    color = Operator(tokens, i, RgbOperands);
                    break;
                }

                case "k":
                {
                    color = Operator(tokens, i, CmykOperands);
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        return new(name, size, color);
    }

    /// <summary>Reads a value as the characters of a string or name.</summary>
    /// <param name="value">The resolved value.</param>
    /// <returns>The characters; empty when the value is not a string.</returns>
    private static string Latin1(PdfValue value) => value.Kind == PdfKind.String ? Encoding.Latin1.GetString(value.AsStringBytes()) : string.Empty;

    /// <summary>Builds a colour operator from the operands before it.</summary>
    /// <param name="tokens">The tokens.</param>
    /// <param name="index">The operator's position.</param>
    /// <param name="operands">How many operands it takes.</param>
    /// <returns>The operator text; black when operands are missing.</returns>
    private static string Operator(string[] tokens, int index, int operands)
    {
        if (index < operands)
        {
            return BlackText;
        }

        var text = new StringBuilder();
        for (var i = index - operands; i <= index; i++)
        {
            _ = text.Append(i == index - operands ? string.Empty : " ");
            _ = text.Append(i == index ? tokens[i] : FormatNumber(ParseNumber(tokens[i])));
        }

        return text.ToString();
    }

    /// <summary>Removes the slash of a name token and decodes its escapes.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The name.</returns>
    private static string DecodeName(string token)
    {
        var name = token.StartsWith('/') ? token[1..] : token;
        if (!name.Contains('#', StringComparison.Ordinal))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            if (name[i] == '#' && i + HexLength < name.Length && int.TryParse(name.AsSpan(i + 1, HexLength), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
            {
                _ = builder.Append((char)code);
                i += HexLength;
            }
            else
            {
                _ = builder.Append(name[i]);
            }
        }

        return builder.ToString();
    }

    /// <summary>Parses a number, treating anything else as zero.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The number.</returns>
    private static float ParseNumber(string token) =>
        float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

    /// <summary>Formats a number for a content stream.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string FormatNumber(float value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
