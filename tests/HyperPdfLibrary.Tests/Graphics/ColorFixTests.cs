// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Tests for the CMYK table, calibrated colour spaces and default colour spaces.</summary>
public sealed class ColorFixTests
{
    /// <summary>The tolerance for float colours.</summary>
    private const float Tolerance = 0.02F;

    /// <summary>The tolerance for colours that must match exactly to a byte.</summary>
    private const float ByteTolerance = 1F / 255F;

    /// <summary>A mid sample, 128 / 255, which lands exactly on grid point 4 of PDFium's CMYK table.</summary>
    private const float MidSample = 128F / 255F;

    /// <summary>The byte scale.</summary>
    private const float ByteScale = 255F;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The index of the red byte in a BGRA pixel.</summary>
    private const int RedByte = 2;

    /// <summary>A gamma value that CalGray must ignore.</summary>
    private const int AnyGamma = 2;

    /// <summary>The largest L* value.</summary>
    private const float MaxLightness = 100;

    /// <summary>The components of CMYK.</summary>
    private const int CmykComponents = 4;

    /// <summary>The components of RGB.</summary>
    private const int RgbComponents = 3;

    /// <summary>The pixels in the cache round-trip row.</summary>
    private const int RowPixels = 600;

    /// <summary>The seed of the pixel generator.</summary>
    private const uint Seed = 99;

    /// <summary>The multiplier of the pixel generator.</summary>
    private const uint Multiplier = 1_103_515_245;

    /// <summary>The increment of the pixel generator.</summary>
    private const uint Increment = 12_345;

    /// <summary>The shift that takes well-mixed bits from the generator.</summary>
    private const int ByteShift = 16;

    /// <summary>The light level of the linear gray test, which sRGB encodes to about 0.735.</summary>
    private const float LinearHalf = 0.5F;

    /// <summary>The sRGB encoding of a linear one half.</summary>
    private const float EncodedHalf = 0.7354F;

    /// <summary>The D65 white point.</summary>
    private static readonly float[] D65 = [0.9505F, 1F, 1.089F];

    /// <summary>The D50 white point.</summary>
    private static readonly float[] D50 = [0.9642F, 1F, 0.8249F];

    /// <summary>The CalRGB matrix of the sRGB primaries, column by column.</summary>
    private static readonly float[] SrgbMatrix = [0.4124F, 0.2126F, 0.0193F, 0.3576F, 0.7152F, 0.1192F, 0.1805F, 0.0722F, 0.9505F];

    /// <summary>Grid points of PDFium's CMYK table come out exactly: a mid sample row converts to the table's bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykRowMatchesPdfiumTableGridPoints()
    {
        // Samples of 0 and 128 sit on grid points 0 and 4. Expected values are kCMYK entries from PDFium.
        var samples = new byte[] { 0x80, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x80, 0x80, 0x80, 0x80, 0x00, 0x00, 0x00, 0x00 };
        byte[][] expected =
        [
            FixTestHelpers.Bgra(0x6B, 0xCF, 0xF6),
            FixTestHelpers.Bgra(0xF4, 0x99, 0xC2),
            FixTestHelpers.Bgra(0x93, 0x95, 0x98),
            FixTestHelpers.Bgra(0x52, 0x4A, 0x47),
            FixTestHelpers.Bgra(0xFF, 0xFF, 0xFF),
        ];
        var bgra = new byte[expected.Length * BytesPerPixel];
        PdfColorSpace.DeviceCmyk.ConvertRow(samples, bgra, expected.Length);

        for (var i = 0; i < expected.Length; i++)
        {
            await Assert.That(bgra.AsSpan(i * BytesPerPixel, BytesPerPixel).ToArray()).IsEquivalentTo(expected[i]);
        }
    }

    /// <summary>Per-colour conversion gives the same table values as row conversion.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykColorMatchesPdfiumTableGridPoints()
    {
        var cyan = ToRgb(PdfColorSpace.DeviceCmyk, MidSample, 0, 0, 0);
        var black = ToRgb(PdfColorSpace.DeviceCmyk, MidSample, MidSample, MidSample, MidSample);

        await Assert.That(cyan[0]).IsEqualTo(0x6B / ByteScale).Within(ByteTolerance);
        await Assert.That(cyan[1]).IsEqualTo(0xCF / ByteScale).Within(ByteTolerance);
        await Assert.That(cyan[2]).IsEqualTo(0xF6 / ByteScale).Within(ByteTolerance);
        await Assert.That(black[0]).IsEqualTo(0x52 / ByteScale).Within(ByteTolerance);
    }

    /// <summary>Row conversion and per-colour conversion agree for off-grid colours.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykRowAgreesWithColorConversion()
    {
        var samples = RandomBytes(RowPixels * CmykComponents);
        var bgra = new byte[RowPixels * BytesPerPixel];
        PdfColorSpace.DeviceCmyk.ConvertRow(samples, bgra, RowPixels);
        var rgb = new float[RgbComponents];
        var worst = 0;
        for (var p = 0; p < RowPixels; p++)
        {
            var colors = new float[CmykComponents];
            for (var c = 0; c < CmykComponents; c++)
            {
                colors[c] = samples[(p * CmykComponents) + c] / ByteScale;
            }

            PdfColorSpace.DeviceCmyk.ToRgb(colors, rgb);
            worst = Math.Max(worst, Math.Abs(bgra[(p * BytesPerPixel) + RedByte] - (int)MathF.Round(rgb[0] * ByteScale)));
            worst = Math.Max(worst, Math.Abs(bgra[(p * BytesPerPixel) + 1] - (int)MathF.Round(rgb[1] * ByteScale)));
            worst = Math.Max(worst, Math.Abs(bgra[p * BytesPerPixel] - (int)MathF.Round(rgb[2] * ByteScale)));
        }

        await Assert.That(worst).IsEqualTo(0);
    }

    /// <summary>CalRGB applies gamma, the matrix and the white point: sRGB primaries give sRGB colours.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CalRgbAppliesMatrixAndWhitePoint()
    {
        var space = PdfColorSpace.Parse(CalRgb([1F, 1F, 1F], SrgbMatrix, D65), null);
        var white = ToRgb(space, 1, 1, 1);
        var red = ToRgb(space, 1, 0, 0);
        var half = ToRgb(space, LinearHalf, LinearHalf, LinearHalf);

        await Assert.That(white[0]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(white[2]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(red[0]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(red[1]).IsEqualTo(0).Within(Tolerance);
        await Assert.That(half[1]).IsEqualTo(EncodedHalf).Within(Tolerance);
    }

    /// <summary>CalRGB rows come from the same maths as single colours, including repeated cached pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CalRgbRowMatchesColorConversion()
    {
        var space = PdfColorSpace.Parse(CalRgb([1F, 1F, 1F], SrgbMatrix, D65), null);
        var samples = RandomBytes(RowPixels * RgbComponents);
        var first = new byte[RowPixels * BytesPerPixel];
        var second = new byte[RowPixels * BytesPerPixel];
        space.ConvertRow(samples, first, RowPixels);
        space.ConvertRow(samples, second, RowPixels);
        var rgb = new float[RgbComponents];
        space.ToRgb([samples[0] / ByteScale, samples[1] / ByteScale, samples[2] / ByteScale], rgb);

        await Assert.That(second).IsEquivalentTo(first);
        await Assert.That(Math.Abs(first[RedByte] - (rgb[0] * ByteScale))).IsLessThanOrEqualTo(1F);
    }

    /// <summary>CalGray returns the gray value unchanged, as PDFium does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CalGrayReturnsGrayUnchanged()
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.Gamma, PdfValue.FromInteger(AnyGamma));
        var space = PdfColorSpace.Parse(FixTestHelpers.Array(FixTestHelpers.Name(KnownName.CalGray), PdfValue.FromDictionary(dictionary)), null);
        var gray = ToRgb(space, LinearHalf);
        var bgra = new byte[BytesPerPixel];
        space.ConvertRow([0x80], bgra, 1);

        await Assert.That(gray[0]).IsEqualTo(LinearHalf);
        await Assert.That(bgra).IsEquivalentTo(FixTestHelpers.Bgra(0x80, 0x80, 0x80));
    }

    /// <summary>Lab maps its dictionary white point to white, whichever white point it has.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LabUsesDictionaryWhitePoint()
    {
        var d50 = PdfColorSpace.Parse(Lab(D50), null);
        var d65 = PdfColorSpace.Parse(Lab(D65), null);
        var white50 = ToRgb(d50, MaxLightness, 0, 0);
        var white65 = ToRgb(d65, MaxLightness, 0, 0);
        var black = ToRgb(d50, 0, 0, 0);

        await Assert.That(white50[0]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(white50[1]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(white50[2]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(white65[2]).IsEqualTo(1).Within(Tolerance);
        await Assert.That(black[1]).IsEqualTo(0).Within(Tolerance);
    }

    /// <summary>Lab rows reuse cached conversions and still match single-colour conversion.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LabRowMatchesColorConversionAfterCaching()
    {
        var space = PdfColorSpace.Parse(Lab(D50), null);
        var samples = RandomBytes(RowPixels * RgbComponents);
        var first = new byte[RowPixels * BytesPerPixel];
        var second = new byte[RowPixels * BytesPerPixel];
        space.ConvertRow(samples, first, RowPixels);
        space.ConvertRow(samples, second, RowPixels);

        await Assert.That(second).IsEquivalentTo(first);
    }

    /// <summary>A name that matches /DefaultRGB in the resources picks up that space, and the default does not recurse.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultRgbReplacesDeviceRgb()
    {
        using var store = FixTestHelpers.Store();
        var resources = new PdfDictionary(store);
        var defaultRgb = store.Names.Intern("DefaultRGB");
        resources.Add(defaultRgb, CalRgb([1F, 1F, 1F], SrgbMatrix, D65));
        var recursive = new PdfDictionary(store);
        recursive.Add(defaultRgb, FixTestHelpers.Name(KnownName.DeviceRGB));

        var replaced = PdfColorSpace.Parse(FixTestHelpers.Name(KnownName.DeviceRGB), resources);
        var plain = PdfColorSpace.Parse(FixTestHelpers.Name(KnownName.DeviceRGB), null);
        var looped = PdfColorSpace.Parse(FixTestHelpers.Name(KnownName.DeviceRGB), recursive);
        var gray = PdfColorSpace.Parse(FixTestHelpers.Name(KnownName.DeviceGray), resources);

        await Assert.That(replaced.Kind).IsEqualTo(PdfColorSpaceKind.CalRgb);
        await Assert.That(plain).IsSameReferenceAs(PdfColorSpace.DeviceRgb);
        await Assert.That(looped).IsSameReferenceAs(PdfColorSpace.DeviceRgb);
        await Assert.That(gray).IsSameReferenceAs(PdfColorSpace.DeviceGray);
    }

    /// <summary>Creates a CalRGB colour space array value.</summary>
    /// <param name="gamma">The gamma values.</param>
    /// <param name="matrix">The matrix.</param>
    /// <param name="white">The white point.</param>
    /// <returns>The value.</returns>
    private static PdfValue CalRgb(float[] gamma, float[] matrix, float[] white)
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.WhitePoint, FixTestHelpers.Numbers(white));
        dictionary.Add(KnownName.Gamma, FixTestHelpers.Numbers(gamma));
        dictionary.Add(KnownName.Matrix, FixTestHelpers.Numbers(matrix));
        return FixTestHelpers.Array(FixTestHelpers.Name(KnownName.CalRGB), PdfValue.FromDictionary(dictionary));
    }

    /// <summary>Creates a Lab colour space array value with the default ranges.</summary>
    /// <param name="white">The white point.</param>
    /// <returns>The value.</returns>
    private static PdfValue Lab(float[] white)
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.WhitePoint, FixTestHelpers.Numbers(white));
        return FixTestHelpers.Array(FixTestHelpers.Name(KnownName.Lab), PdfValue.FromDictionary(dictionary));
    }

    /// <summary>Converts a colour to RGB.</summary>
    /// <param name="space">The space.</param>
    /// <param name="components">The components.</param>
    /// <returns>The RGB values.</returns>
    private static float[] ToRgb(PdfColorSpace space, params float[] components)
    {
        var rgb = new float[RgbComponents];
        space.ToRgb(components, rgb);
        return rgb;
    }

    /// <summary>Creates random bytes from a fixed seed.</summary>
    /// <param name="length">The length.</param>
    /// <returns>The bytes.</returns>
    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        var state = Seed;
        for (var i = 0; i < bytes.Length; i++)
        {
            state = unchecked((state * Multiplier) + Increment);
            bytes[i] = (byte)(state >>> ByteShift);
        }

        return bytes;
    }
}
