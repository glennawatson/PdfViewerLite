// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>Fills a buffer through a PDFium two-call string API.</summary>
/// <param name="buffer">The buffer.</param>
/// <param name="length">The buffer length in bytes.</param>
/// <returns>The length PDFium reported.</returns>
internal unsafe delegate CULong NativeStringReader(byte* buffer, CULong length);
