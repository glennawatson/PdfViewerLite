// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>One image channel after the JP2 palette and channel definitions: a component plane, maybe through a palette column.</summary>
/// <param name="Component">The component plane.</param>
/// <param name="Column">The palette column, or -1 when the plane holds the values themselves.</param>
/// <param name="Precision">The bits per value.</param>
/// <param name="Signed">Whether the values are signed.</param>
internal readonly record struct JpxChannel(int Component, int Column, int Precision, bool Signed);
