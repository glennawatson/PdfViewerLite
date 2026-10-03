// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Core.Annotations;

/// <summary>
/// The annotation colours offered: soft tones that keep text readable when multiplied over it (docs/COMFORT.md).
/// Each has a name, so colour is never the only way to tell them apart.
/// </summary>
public static class AnnotationColors
{
    /// <summary>Gets soft yellow, the default highlight.</summary>
    public static uint Sand => 0xF0DC96U;

    /// <summary>Gets soft green.</summary>
    public static uint Sage => 0xBCDCB0U;

    /// <summary>Gets soft blue.</summary>
    public static uint Slate => 0xB4CCDCU;

    /// <summary>Gets soft red.</summary>
    public static uint Clay => 0xE8BCB4U;

    /// <summary>Gets the dark ink used for drawings, text boxes and signatures.</summary>
    public static uint Ink => 0x23324AU;

    /// <summary>Gets the named colours in menu order.</summary>
    public static IReadOnlyList<(string Name, uint Color)> All { get; } = [("Yellow", Sand), ("Green", Sage), ("Blue", Slate), ("Red", Clay)];
}
