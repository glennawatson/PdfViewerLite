// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>The family of a colour space (PDF 32000 section 8.6).</summary>
public enum PdfColorSpaceKind
{
    /// <summary>The DeviceGray family.</summary>
    DeviceGray = 0,

    /// <summary>The DeviceRGB family.</summary>
    DeviceRgb = 1,

    /// <summary>The DeviceCMYK family.</summary>
    DeviceCmyk = 2,

    /// <summary>The CalGray family.</summary>
    CalGray = 3,

    /// <summary>The CalRGB family.</summary>
    CalRgb = 4,

    /// <summary>The Lab family.</summary>
    Lab = 5,

    /// <summary>The ICCBased family.</summary>
    IccBased = 6,

    /// <summary>The Indexed family.</summary>
    Indexed = 7,

    /// <summary>The Separation family.</summary>
    Separation = 8,

    /// <summary>The DeviceN family.</summary>
    DeviceN = 9,

    /// <summary>The Pattern family.</summary>
    Pattern = 10,
}
