// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Editing;

/// <summary>One XMP property to set or remove.</summary>
/// <param name="Property">The property.</param>
/// <param name="Value">The text; empty removes the property.</param>
/// <param name="Language">For a language alternative, the language whose entry is replaced besides <c>x-default</c>, or null for <c>x-default</c> alone.</param>
[DebuggerDisplay("XmpChange: {Property.LocalName}")]
internal readonly record struct XmpChange(XmpProperty Property, string Value, string? Language)
{
    /// <summary>Initializes a new instance of the <see cref="XmpChange"/> struct that changes <c>x-default</c> only.</summary>
    /// <param name="property">The property.</param>
    /// <param name="value">The text; empty removes the property.</param>
    internal XmpChange(XmpProperty property, string value)
        : this(property, value, null)
    {
    }
}
