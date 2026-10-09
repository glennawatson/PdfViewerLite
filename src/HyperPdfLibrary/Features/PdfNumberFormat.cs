// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>How to show a measured value in one unit (a number format dictionary).</summary>
/// <param name="Unit">The unit label, for example "mi" (<c>/U</c>).</param>
/// <param name="Conversion">The factor from the previous unit to this one (<c>/C</c>).</param>
/// <param name="Format">The number format: D (decimal), F (fraction), R (round) or T (truncate).</param>
/// <param name="Precision">The decimal precision, or the fraction denominator (<c>/D</c>).</param>
/// <param name="Fractional">Whether a decimal value is shown as a fraction when it is close (<c>/FD</c>).</param>
/// <param name="ThousandsSeparator">The thousands separator (<c>/RT</c>).</param>
/// <param name="DecimalSeparator">The decimal separator (<c>/RD</c>).</param>
/// <param name="Prefix">The text before the value (<c>/PS</c>).</param>
/// <param name="Suffix">The text after the value (<c>/SS</c>).</param>
/// <param name="LabelPosition">Where the unit label goes relative to the value: S (suffix) or P (prefix).</param>
[DebuggerDisplay("PdfNumberFormat: {Unit}")]
public sealed record PdfNumberFormat(
    string Unit,
    double Conversion,
    string Format,
    int Precision,
    bool Fractional,
    string ThousandsSeparator,
    string DecimalSeparator,
    string Prefix,
    string Suffix,
    string LabelPosition);
