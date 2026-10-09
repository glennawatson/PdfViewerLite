// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>One component of a JPEG 2000 image, as the SIZ marker describes it.</summary>
/// <param name="Precision">The bits per sample, 1 to 31.</param>
/// <param name="Signed">Whether samples are signed.</param>
/// <param name="Dx">The horizontal subsampling factor on the reference grid.</param>
/// <param name="Dy">The vertical subsampling factor on the reference grid.</param>
internal readonly record struct JpxComponentInfo(int Precision, bool Signed, int Dx, int Dy);
