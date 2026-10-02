// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>Native <c>FS_QUADPOINTSF</c>: the four corners of a marked line of text, top-left, top-right, bottom-left, bottom-right.</summary>
/// <param name="X1">The first x.</param>
/// <param name="Y1">The first y.</param>
/// <param name="X2">The second x.</param>
/// <param name="Y2">The second y.</param>
/// <param name="X3">The third x.</param>
/// <param name="Y3">The third y.</param>
/// <param name="X4">The fourth x.</param>
/// <param name="Y4">The fourth y.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct FsQuadPointsF(float X1, float Y1, float X2, float Y2, float X3, float Y3, float X4, float Y4);
