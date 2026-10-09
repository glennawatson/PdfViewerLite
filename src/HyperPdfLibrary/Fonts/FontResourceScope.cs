// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>The resources visible inside a content stream.</summary>
/// <param name="Dictionary">The innermost resources.</param>
/// <param name="Parent">The inherited resources.</param>
internal sealed record FontResourceScope(PdfDictionary? Dictionary, FontResourceScope? Parent)
{
    /// <summary>Gets the number of visible resource scopes.</summary>
    internal int Depth { get; } = (Parent?.Depth ?? 0) + 1;

    /// <summary>Finds a named resource through the visible scopes.</summary>
    /// <param name="category">The resource category.</param>
    /// <param name="name">The resource name.</param>
    /// <returns>The resolved value, or null.</returns>
    internal PdfValue Find(KnownName category, PdfName name)
    {
        for (FontResourceScope? scope = this; scope is not null; scope = scope.Parent)
        {
            var value = scope.Dictionary?.GetDictionary(category)?.Get(name) ?? default;
            if (!value.IsNull)
            {
                return value;
            }
        }

        return default;
    }
}
