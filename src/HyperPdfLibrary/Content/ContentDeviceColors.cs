// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Content;

/// <summary>Runs the interpreter's DeviceColors operations over its owned state.</summary>
internal static class ContentDeviceColors
{
    /// <summary>Gets the space a device colour converts through: the output intent profile when the cache carries one, otherwise the device space.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "space">The device space the content named.</param>
    /// <returns>The space to resolve colours in.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfColorSpace MapDeviceSpace(ContentInterpreter self, PdfColorSpace space) => self.Cache.DeviceColors is { } colors ? colors.Map(space) : space;
}
