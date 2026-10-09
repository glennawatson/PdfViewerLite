// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Data;

/// <summary>One of the standard 14 fonts every PDF reader provides.</summary>
public enum StandardFont
{
    /// <summary>Not a standard font.</summary>
    None = 0,

    /// <summary>The Courier font.</summary>
    Courier = 1,

    /// <summary>The Courier-Bold font.</summary>
    CourierBold = 2,

    /// <summary>The Courier-BoldOblique font.</summary>
    CourierBoldOblique = 3,

    /// <summary>The Courier-Oblique font.</summary>
    CourierOblique = 4,

    /// <summary>The Helvetica font.</summary>
    Helvetica = 5,

    /// <summary>The Helvetica-Bold font.</summary>
    HelveticaBold = 6,

    /// <summary>The Helvetica-BoldOblique font.</summary>
    HelveticaBoldOblique = 7,

    /// <summary>The Helvetica-Oblique font.</summary>
    HelveticaOblique = 8,

    /// <summary>The Times-Roman font.</summary>
    TimesRoman = 9,

    /// <summary>The Times-Bold font.</summary>
    TimesBold = 10,

    /// <summary>The Times-BoldItalic font.</summary>
    TimesBoldItalic = 11,

    /// <summary>The Times-Italic font.</summary>
    TimesItalic = 12,

    /// <summary>The Symbol font.</summary>
    Symbol = 13,

    /// <summary>The ZapfDingbats font.</summary>
    ZapfDingbats = 14,
}
