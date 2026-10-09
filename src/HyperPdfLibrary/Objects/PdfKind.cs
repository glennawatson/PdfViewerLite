// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Objects;

/// <summary>The type of a PDF value.</summary>
public enum PdfKind
{
    /// <summary>The null object, also used for missing values.</summary>
    Null = 0,

    /// <summary>A boolean.</summary>
    Boolean = 1,

    /// <summary>An integer.</summary>
    Integer = 2,

    /// <summary>A real number.</summary>
    Real = 3,

    /// <summary>A name.</summary>
    Name = 4,

    /// <summary>A string, already unescaped and decrypted.</summary>
    String = 5,

    /// <summary>An array.</summary>
    Array = 6,

    /// <summary>A dictionary.</summary>
    Dictionary = 7,

    /// <summary>A stream: a dictionary with data.</summary>
    Stream = 8,

    /// <summary>A reference to an indirect object.</summary>
    Reference = 9,
}
