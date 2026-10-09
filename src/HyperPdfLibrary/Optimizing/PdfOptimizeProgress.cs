// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Optimizing;

/// <summary>How far an optimisation run has got, for a progress bar.</summary>
/// <param name="Phase">The current step.</param>
/// <param name="Completed">The items of the step done so far: pages while analysing, objects while writing.</param>
/// <param name="Total">The items in the step; zero when unknown.</param>
/// <param name="BytesWritten">The bytes written to the destination so far.</param>
[DebuggerDisplay("PdfOptimizeProgress: {Phase} {Completed}/{Total}")]
public readonly record struct PdfOptimizeProgress(PdfOptimizePhase Phase, int Completed, int Total, long BytesWritten)
{
    /// <summary>Gets the share of the step done, from 0 to 1; 0 when the total is unknown.</summary>
    public double Fraction => Total > 0 ? Math.Clamp((double)Completed / Total, 0, 1) : 0;
}
