// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Content;

/// <content>Output intent substitution for the device colour spaces.</content>
internal sealed partial class ContentInterpreter
{
    /// <summary>Gets the space a device colour converts through: the output intent profile when the cache carries one, otherwise the device space.</summary>
    /// <param name="space">The device space the content named.</param>
    /// <returns>The space to resolve colours in.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PdfColorSpace MapDeviceSpace(PdfColorSpace space) => _cache.DeviceColors is { } colors ? colors.Map(space) : space;
}
