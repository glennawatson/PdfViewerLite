// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The bit depth an HT cleanup pass decodes to.</summary>
/// <param name="Planes">The code-block's bit-planes p: the cleanup pass's lowest magnitude bit sits at twice scale bit p.</param>
/// <param name="Limit">The largest exponent bound U a quad may have.</param>
internal readonly record struct JpxHtDepth(int Planes, int Limit);
