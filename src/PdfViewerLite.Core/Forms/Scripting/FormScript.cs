// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace PdfViewerLite.Core.Forms.Scripting;

/// <summary>
/// A form field script recognised as one of Acrobat's built-in form functions, with its arguments. Only these calls,
/// and simplified field notation calculations, are understood; any other JavaScript is not run, so a document cannot
/// run code on the reader's computer.
/// </summary>
/// <param name="Function">The function.</param>
/// <param name="Arguments">The literal arguments in order: numbers, strings and booleans as written.</param>
/// <param name="Fields">The field names a calculation reads.</param>
/// <param name="Expression">The simplified field notation of an expression calculation, otherwise empty.</param>
[DebuggerDisplay("{Function}({Arguments.Count} arguments)")]
public sealed partial record FormScript(FormScriptFunction Function, IReadOnlyList<string> Arguments, IReadOnlyList<string> Fields, string Expression)
{
    /// <summary>Gets the script used when there is none, or it is not understood.</summary>
    public static FormScript None { get; } = new(FormScriptFunction.Unknown, [], [], string.Empty);

    /// <summary>Recognises a script.</summary>
    /// <param name="javaScript">The field action's JavaScript.</param>
    /// <returns>The recognised script, or <see cref="None"/>.</returns>
    public static FormScript Parse(string? javaScript)
    {
        if (string.IsNullOrWhiteSpace(javaScript))
        {
            return None;
        }

        // Acrobat writes simplified field notation between these markers, ahead of the JavaScript it generates.
        var notation = NotationPattern().Match(javaScript);
        if (notation.Success)
        {
            return new(FormScriptFunction.Expression, [], [], notation.Groups["expression"].Value.Trim());
        }

        var call = CallPattern().Match(javaScript);
        if (!call.Success)
        {
            return None;
        }

        var function = FunctionOf(call.Groups["name"].Value);
        if (function == FormScriptFunction.Unknown)
        {
            return None;
        }

        var arguments = new List<string>();
        var fields = new List<string>();
        ReadArguments(call.Groups["args"].Value, arguments, fields);
        if (function == FormScriptFunction.Simple && fields.Count == 0 && arguments.Count > 1)
        {
            // The fields can also be one comma-separated string.
            fields.AddRange(arguments[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        }

        return new(function, arguments, fields, string.Empty);
    }

    /// <summary>Gets an argument as a number, or a fallback when it is missing or not a number.</summary>
    /// <param name="index">The argument's position.</param>
    /// <param name="fallback">The fallback.</param>
    /// <returns>The number.</returns>
    public double Number(int index, double fallback) =>
        index < Arguments.Count && double.TryParse(Arguments[index], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : fallback;

    /// <summary>Gets an argument as text, or a fallback when it is missing.</summary>
    /// <param name="index">The argument's position.</param>
    /// <param name="fallback">The fallback.</param>
    /// <returns>The text.</returns>
    public string Text(int index, string fallback) => index < Arguments.Count ? Arguments[index] : fallback;

    /// <summary>Gets an argument as a flag: true, or a non-zero number.</summary>
    /// <param name="index">The argument's position.</param>
    /// <returns>The flag; false when missing.</returns>
    public bool Flag(int index) => index < Arguments.Count && Arguments[index] is "true" or not ("false" or "0" or "");

    /// <summary>Maps a built-in form function's name to what it does.</summary>
    /// <param name="name">The function's name.</param>
    /// <returns>The function, or unknown.</returns>
    private static FormScriptFunction FunctionOf(string name) => name switch
    {
        "AFNumber_Format" or "AFNumber_Keystroke" => FormScriptFunction.Number,
        "AFPercent_Format" or "AFPercent_Keystroke" => FormScriptFunction.Percent,
        "AFDate_FormatEx" or "AFDate_KeystrokeEx" => FormScriptFunction.Date,
        "AFSpecial_Format" or "AFSpecial_Keystroke" => FormScriptFunction.Special,
        "AFRange_Validate" => FormScriptFunction.Range,
        "AFSimple_Calculate" => FormScriptFunction.Simple,
        _ => FormScriptFunction.Unknown,
    };

    /// <summary>Splits a call's arguments into literals, gathering the strings of a <c>new Array(...)</c> as field names.</summary>
    /// <param name="text">The text between the call's parentheses.</param>
    /// <param name="arguments">Receives the literals.</param>
    /// <param name="fields">Receives the field names of an array.</param>
    private static void ReadArguments(string text, List<string> arguments, List<string> fields)
    {
        foreach (var token in (IEnumerable<Match>)TokenPattern().Matches(text))
        {
            if (token.Groups["array"].Success)
            {
                foreach (var name in (IEnumerable<Match>)StringPattern().Matches(token.Groups["array"].Value))
                {
                    fields.Add(Unescape(name.Groups["body"].Value));
                }

                arguments.Add(string.Empty);
            }
            else if (token.Groups["string"].Success)
            {
                arguments.Add(Unescape(token.Groups["string"].Value));
            }
            else
            {
                arguments.Add(token.Groups["literal"].Value);
            }
        }
    }

    /// <summary>Removes JavaScript string escapes.</summary>
    /// <param name="text">The escaped text.</param>
    /// <returns>The text.</returns>
    private static string Unescape(string text)
    {
        if (!text.Contains('\\', StringComparison.Ordinal))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                i++;
            }

            _ = builder.Append(text[i]);
        }

        return builder.ToString();
    }

    /// <summary>Matches simplified field notation between Acrobat's markers.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"BVCALC(?<expression>.*?)EVCALC", RegexOptions.Singleline)]
    private static partial Regex NotationPattern();

    /// <summary>Matches the first call of a built-in form function.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\b(?<name>AF\w+)\s*\((?<args>(?:[^()""']|""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|\((?:[^()]|""[^""]*"")*\))*)\)")]
    private static partial Regex CallPattern();

    /// <summary>Matches one argument: an array of strings, a string, or a bare literal.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"new\s+Array\s*\((?<array>[^)]*)\)|""(?<string>(?:\\.|[^""\\])*)""|'(?<string>(?:\\.|[^'\\])*)'|(?<literal>[^,\s][^,]*?)(?=\s*(?:,|$))")]
    private static partial Regex TokenPattern();

    /// <summary>Matches a string literal.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"""(?<body>(?:\\.|[^""\\])*)""|'(?<body>(?:\\.|[^'\\])*)'")]
    private static partial Regex StringPattern();
}
