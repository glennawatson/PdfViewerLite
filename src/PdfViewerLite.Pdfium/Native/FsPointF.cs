// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>Native <c>FS_POINTF</c>.</summary>
/// <param name="X">The x coordinate.</param>
/// <param name="Y">The y coordinate.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct FsPointF(float X, float Y);
