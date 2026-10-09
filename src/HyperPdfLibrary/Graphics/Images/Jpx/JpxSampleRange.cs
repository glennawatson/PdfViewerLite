// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>A component's DC level shift and sample range.</summary>
/// <param name="Shift">The level shift added: half the range for unsigned samples, zero for signed ones.</param>
/// <param name="Minimum">The smallest sample.</param>
/// <param name="Maximum">The largest sample.</param>
internal readonly record struct JpxSampleRange(int Shift, int Minimum, int Maximum)
{
    /// <summary>Gets the range of a component.</summary>
    /// <param name="component">The component.</param>
    /// <returns>The range.</returns>
    internal static JpxSampleRange For(in JpxComponentInfo component)
    {
        var half = 1L << (component.Precision - 1);
        return component.Signed
            ? new(0, (int)-half, (int)(half - 1))
            : new((int)half, 0, (int)((half << 1) - 1));
    }
}
