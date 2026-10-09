// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Reads form field names and inherited field types by walking /Parent.</summary>
internal static class PdfFieldNames
{
    /// <summary>The deepest field tree walked.</summary>
    private const int MaxDepth = 32;

    /// <summary>Gets a field's fully qualified name: the partial names (/T) of it and its parents, joined with dots.</summary>
    /// <param name="field">The field or widget dictionary.</param>
    /// <returns>The name, or an empty string when no level has a /T.</returns>
    internal static string FullName(PdfDictionary field)
    {
        var parts = new List<string>();
        for (var node = field; node is not null && parts.Count < MaxDepth; node = node.GetDictionary(KnownName.Parent))
        {
            if (node.GetText(KnownName.T) is { Length: > 0 } partial)
            {
                parts.Add(partial);
            }
        }

        if (parts.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var i = parts.Count - 1; i >= 0; i--)
        {
            _ = builder.Append(parts[i]);
            if (i > 0)
            {
                _ = builder.Append('.');
            }
        }

        return builder.ToString();
    }

    /// <summary>Gets a field's type (/FT), inherited from its parents.</summary>
    /// <param name="field">The field or widget dictionary.</param>
    /// <returns>The type, or no name.</returns>
    internal static PdfName FieldType(PdfDictionary field)
    {
        var depth = 0;
        for (var node = field; node is not null && depth < MaxDepth; node = node.GetDictionary(KnownName.Parent))
        {
            var type = node.GetName(KnownName.FT);
            if (!type.IsNone)
            {
                return type;
            }

            depth++;
        }

        return default;
    }
}
