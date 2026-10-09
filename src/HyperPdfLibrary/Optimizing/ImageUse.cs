// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Optimizing;

/// <summary>How an image is used across the document.</summary>
/// <param name="MinPpi">The lowest resolution it is drawn at, in pixels per inch: its largest use. Infinity when never measured.</param>
/// <param name="Uses">The times it is drawn by page content.</param>
/// <param name="IsFixed">
/// Whether it is also used where its size is not known, such as in a pattern, an annotation or a soft mask, so it must
/// keep its resolution.
/// </param>
[DebuggerDisplay("ImageUse: {MinPpi} ppi, {Uses} uses, fixed {IsFixed}")]
internal readonly record struct ImageUse(float MinPpi, int Uses, bool IsFixed)
{
    /// <summary>Gets the state of an image not seen yet.</summary>
    internal static ImageUse Unseen => new(float.PositiveInfinity, 0, false);
}
