// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Describes a native soft-mask layer opened around one painting operation.</summary>
/// <param name="Active">Whether a layer was opened.</param>
/// <param name="Bounds">The native layer bounds.</param>
internal readonly record struct SkiaMaskScope(bool Active, SKRect Bounds);
