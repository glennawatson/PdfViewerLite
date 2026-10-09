// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Annotations;

/// <summary>The twelve Latin fonts every PDF reader has built in, which appearances can use without embedding a font.</summary>
public enum AppearanceFont
{
    /// <summary>Helvetica, the sans serif face.</summary>
    Helvetica = 0,

    /// <summary>Helvetica bold.</summary>
    HelveticaBold = 1,

    /// <summary>Helvetica oblique.</summary>
    HelveticaOblique = 2,

    /// <summary>Helvetica bold oblique.</summary>
    HelveticaBoldOblique = 3,

    /// <summary>Times roman.</summary>
    TimesRoman = 4,

    /// <summary>Times bold.</summary>
    TimesBold = 5,

    /// <summary>Times italic.</summary>
    TimesItalic = 6,

    /// <summary>Times bold italic.</summary>
    TimesBoldItalic = 7,

    /// <summary>Courier, the fixed width face.</summary>
    Courier = 8,

    /// <summary>Courier bold.</summary>
    CourierBold = 9,

    /// <summary>Courier oblique.</summary>
    CourierOblique = 10,

    /// <summary>Courier bold oblique.</summary>
    CourierBoldOblique = 11,
}
