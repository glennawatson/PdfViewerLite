// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>PDFium's affine transform matrix.</summary>
/// <param name="A">Horizontal scale.</param>
/// <param name="B">Vertical shear.</param>
/// <param name="C">Horizontal shear.</param>
/// <param name="D">Vertical scale.</param>
/// <param name="E">Horizontal translation.</param>
/// <param name="F">Vertical translation.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct FsMatrix(float A, float B, float C, float D, float E, float F);
