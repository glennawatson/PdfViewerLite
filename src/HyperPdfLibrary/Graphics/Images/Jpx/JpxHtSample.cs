// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>A sample position inside an HT code-block.</summary>
/// <param name="X">The column.</param>
/// <param name="Y">The row.</param>
internal readonly record struct JpxHtSample(int X, int Y);
