// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Interchange;

/// <summary>The value of one terminal form field, named by its fully qualified name.</summary>
[DebuggerDisplay("PdfInterchangeField: {Name} = {Values.Length} values")]
public sealed class PdfInterchangeField
{
    /// <summary>Initializes a new instance of the <see cref="PdfInterchangeField"/> class.</summary>
    /// <param name="name">The fully qualified name: the partial names joined by dots.</param>
    /// <param name="values">The values; a field with one value has one entry, a multiple-choice list has several.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public PdfInterchangeField(string name, string[] values)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(values);
        Name = name;
        Values = values;
    }

    /// <summary>Gets the fully qualified name.</summary>
    public string Name { get; }

    /// <summary>Gets the values.</summary>
    public string[] Values { get; }

    /// <summary>Gets the first value, or <see langword="null"/> when the field has none.</summary>
    public string? Value => Values.Length > 0 ? Values[0] : null;

    /// <summary>Gets or sets the rich text value (XHTML), or <see langword="null"/>.</summary>
    public string? RichText { get; set; }

    /// <summary>Gets or sets a value indicating whether the value is a PDF name, as a check box state is, rather than a string.</summary>
    public bool ValueIsName { get; set; }

    /// <summary>Gets or sets the field flags to set in an FDF file (<c>/SetFf</c>), or <see langword="null"/>.</summary>
    public uint? SetFlags { get; set; }

    /// <summary>Gets or sets the field flags to clear in an FDF file (<c>/ClrFf</c>), or <see langword="null"/>.</summary>
    public uint? ClearFlags { get; set; }

    /// <summary>Gets or sets the field flags an FDF file gives outright (<c>/Ff</c>), or <see langword="null"/>.</summary>
    public uint? Flags { get; set; }
}
