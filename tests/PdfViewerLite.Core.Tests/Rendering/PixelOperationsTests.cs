// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Rendering;

namespace PdfViewerLite.Core.Tests.Rendering;

/// <summary>Tests for <see cref="PixelOperations"/>.</summary>
public sealed class PixelOperationsTests
{
    /// <summary>Verifies colour channels invert and alpha is kept, across vector and scalar paths.</summary>
    /// <param name="length">The pixel count.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(7)]
    [Arguments(64)]
    [Arguments(67)]
    public async Task InvertsColorsKeepingAlpha(int length)
    {
        const uint white = 0xFFFFFFFFU;
        const uint black = 0xFF000000U;
        var pixels = new uint[length];
        Array.Fill(pixels, white);

        PixelOperations.InvertColors(pixels);

        await Assert.That(Array.TrueForAll(pixels, static p => p == black)).IsTrue();
    }
}
