// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using SkiaSharp;

namespace HyperPdfLibrary.Graphics;

/// <summary>A soft mask: its group recorded in page space, and how the group becomes coverage.</summary>
/// <param name="Picture">The mask group, recorded with the transform in force when the mask was set.</param>
/// <param name="IsLuminosity">Whether coverage is the group's luminosity over the backdrop; otherwise its alpha.</param>
/// <param name="Backdrop">The backdrop colour a luminosity mask is composited over.</param>
/// <param name="Transfer">The 256-entry transfer function applied to coverage, or null for identity.</param>
[DebuggerDisplay("PdfSoftMask: luminosity {IsLuminosity}")]
internal sealed record PdfSoftMask(SKPicture Picture, bool IsLuminosity, SKColor Backdrop, byte[]? Transfer);
