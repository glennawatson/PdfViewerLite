// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Rendering;

namespace PdfViewerLite.Core.Tests.Rendering;

/// <summary>Tests for <see cref="PageTone"/>.</summary>
public sealed class PageToneTests
{
    /// <summary>The Calm night paper colour.</summary>
    private const uint CalmPaper = 0x2A2826U;

    /// <summary>The Calm night ink colour.</summary>
    private const uint CalmInk = 0xD2CDC5U;

    /// <summary>The expected paper pixel in BGRA order.</summary>
    private static readonly byte[] PaperBgra = [0x26, 0x28, 0x2A, 0xFF];

    /// <summary>The expected ink pixel in BGRA order.</summary>
    private static readonly byte[] InkBgra = [0xC5, 0xCD, 0xD2, 0xFF];

    /// <summary>Verifies white becomes paper, black becomes ink, mid grey lands between, and alpha is kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MapsPaperAndInk()
    {
        const byte white = 0xFF;
        const byte grey = 0x80;
        const byte alpha = 0xFF;
        byte[] pixels = [white, white, white, alpha, 0, 0, 0, alpha, grey, grey, grey, alpha];
        var tone = new PageTone(CalmPaper, CalmInk);

        tone.Apply(pixels);

        // BGRA order: paper 2A,28,26 is stored as 26,28,2A and ink D2,CD,C5 as C5,CD,D2.
        const int inkPixel = 4;
        const int greyPixel = 8;
        await Assert.That(pixels.AsSpan(0, inkPixel).SequenceEqual(PaperBgra)).IsTrue();
        await Assert.That(pixels.AsSpan(inkPixel, greyPixel - inkPixel).SequenceEqual(InkBgra)).IsTrue();
        await Assert.That(pixels[greyPixel]).IsBetween(PaperBgra[0], InkBgra[0]);
    }

    /// <summary>Verifies the identity tone changes nothing and has identifier 0.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoneIsIdentity()
    {
        const byte value = 0x42;
        byte[] pixels = [value, value, value, value];

        PageTone.None.Apply(pixels);

        await Assert.That(PageTone.None.IsIdentity).IsTrue();
        await Assert.That(PageTone.None.Id).IsEqualTo(0);
        await Assert.That(pixels[0]).IsEqualTo(value);
    }

    /// <summary>Verifies distinct tones get distinct non-zero identifiers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IdentifiersDiffer()
    {
        const uint softPaper = 0xFAF7F0U;
        const uint softInk = 0x282624U;
        var calm = new PageTone(CalmPaper, CalmInk);
        var soft = new PageTone(softPaper, softInk);

        await Assert.That(calm.Id).IsNotEqualTo(0);
        await Assert.That(calm.Id).IsNotEqualTo(soft.Id);
        await Assert.That(new PageTone(CalmPaper, CalmInk).Id).IsEqualTo(calm.Id);
    }

    /// <summary>Verifies the vectorised path gives exactly what the per-pixel path gives, for every channel value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VectorMatchesScalar()
    {
        const int pixelBytes = 4;
        const int values = 256;
        const int tail = 12;
        var tone = new PageTone(CalmPaper, CalmInk);
        var pixels = new byte[(values * pixelBytes) + tail];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(i / pixelBytes);
        }

        var expected = (byte[])pixels.Clone();
        for (var i = 0; i < expected.Length; i += pixelBytes)
        {
            tone.Apply(expected.AsSpan(i, pixelBytes));
        }

        tone.Apply(pixels);

        await Assert.That(pixels.AsSpan().SequenceEqual(expected)).IsTrue();
    }
}
