// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jpx;
using HyperPdfLibrary.Tests.Graphics.Jpeg;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// Lossless round trips of high-throughput (ITU-T Rec. T.814) codestreams from the test HT encoder: the cleanup pass
/// alone, and the cleanup pass with SigProp and MagRef, across block and image sizes, components, subsampling, layers,
/// tiles and precincts.
/// </summary>
public sealed class JpxHighThroughputTests
{
    /// <summary>The components of an RGB image.</summary>
    private const int Rgb = 3;

    /// <summary>The layers of the layered tests.</summary>
    private const int ThreeLayers = 3;

    /// <summary>The chroma subsampling of the subsampled tests.</summary>
    private const int Halved = 2;

    /// <summary>The precinct exponent of the precinct tests.</summary>
    private const int SmallPrecincts = 4;

    /// <summary>The code-block exponent used with small precincts.</summary>
    private const int SmallBlocks = 3;

    /// <summary>The largest code-block exponent: 64 by 64 blocks.</summary>
    private const int LargeBlocks = 6;

    /// <summary>The tile width of the tiled tests.</summary>
    private const int TileWidth = 23;

    /// <summary>The tile height of the tiled tests.</summary>
    private const int TileHeight = 19;

    /// <summary>The width of the large image.</summary>
    private const int LargeWidth = 203;

    /// <summary>The height of the large image.</summary>
    private const int LargeHeight = 157;

    /// <summary>The seed of the noise generator.</summary>
    private const uint Seed = 77;

    /// <summary>One past the largest 8-bit sample.</summary>
    private const int SampleRange = 256;

    /// <summary>The side of a block coded directly.</summary>
    private const int BlockSide = 16;

    /// <summary>The spread of the coefficients of a block coded directly.</summary>
    private const int CoefficientSpread = 40;

    /// <summary>The refinement segment's passes.</summary>
    private const int RefinementPasses = 2;

    /// <summary>The Ccap15 value of the MIXED mode.</summary>
    private const byte MixedHigh = 0xC0;

    /// <summary>The Pcap bytes of a CAP segment.</summary>
    private const int PcapBytes = 4;

    /// <summary>The Rsiz value that says a CAP marker follows.</summary>
    private const int CapRsiz = 0x4000;

    /// <summary>A plain single-component HT codestream decodes exactly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CleanupPassRoundTrips() => await Assert.That(RoundTrips(Ht(new()))).IsTrue();

    /// <summary>Image sizes with partial quads, partial stripes and single rows or columns decode exactly.</summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="blockExponent">The code-block side as a power of two.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, 1, 2)]
    [Arguments(1, 37, 3)]
    [Arguments(37, 1, 3)]
    [Arguments(3, 3, 2)]
    [Arguments(5, 7, 2)]
    [Arguments(13, 11, 4)]
    [Arguments(64, 64, 6)]
    [Arguments(65, 66, 6)]
    [Arguments(130, 9, 5)]
    public async Task SizesRoundTrip(int width, int height, int blockExponent)
    {
        var options = Ht(new() { Width = width, Height = height, BlockExponent = blockExponent, Levels = 1 });

        await Assert.That(RoundTrips(options)).IsTrue();
        await Assert.That(RoundTrips(options with { HtRefinement = true })).IsTrue();
    }

    /// <summary>Full-range noise, whose quads have large exponents and long residuals, decodes exactly.</summary>
    /// <param name="refine">Whether the refinement passes are used.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task NoiseRoundTrips(bool refine)
    {
        var options = Ht(new() { Width = LargeWidth, Height = LargeHeight, BlockExponent = LargeBlocks, HtRefinement = refine });

        await Assert.That(RoundTrips(options, Noise(options))).IsTrue();
    }

    /// <summary>The SigProp and MagRef passes decode exactly, with and without the vertically causal mode.</summary>
    /// <param name="causal">Whether the vertically causal mode is on.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RefinementPassesRoundTrip(bool causal)
    {
        var style = JpxBlockStyle.HighThroughput | (causal ? JpxBlockStyle.VerticallyCausal : JpxBlockStyle.None);
        var options = new JpxTestOptions { Components = Rgb, Transform = true, Style = style, HtRefinement = true };

        await Assert.That(RoundTrips(options)).IsTrue();
    }

    /// <summary>A busy block is coded with the refinement passes, so the round trips above use them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BusyBlocksUseTheRefinementPasses()
    {
        var random = new JpegTestRandom(Seed);
        var coefficients = new int[BlockSide * BlockSide];
        for (var i = 0; i < coefficients.Length; i++)
        {
            coefficients[i] = random.Next(-CoefficientSpread, CoefficientSpread + 1);
        }

        var code = JpxTestHtBlockEncoder.Encode(coefficients, BlockSide, BlockSide, true, false);

        await Assert.That(code.Segments.Count).IsEqualTo(RefinementPasses);
        await Assert.That(code.Segments[1].Passes).IsEqualTo(RefinementPasses);
    }

    /// <summary>RGB with the component transform, and subsampled chroma, decode exactly.</summary>
    /// <param name="subsampling">The chroma subsampling.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(Halved)]
    public async Task ComponentsRoundTrip(int subsampling)
    {
        var options = Ht(new() { Components = Rgb, Transform = subsampling == 1, ChromaSubsampling = subsampling });

        await Assert.That(RoundTrips(options)).IsTrue();
        await Assert.That(RoundTrips(options with { HtRefinement = true })).IsTrue();
    }

    /// <summary>Layers that split a block's cleanup and refinement segments decode exactly in every progression.</summary>
    /// <param name="order">The progression order.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments((int)JpxProgressionOrder.LayerResolutionComponentPosition)]
    [Arguments((int)JpxProgressionOrder.ResolutionPositionComponentLayer)]
    [Arguments((int)JpxProgressionOrder.ComponentPositionResolutionLayer)]
    public async Task LayersRoundTrip(int order)
    {
        var layered = new JpxTestOptions { Components = Rgb, Order = (JpxProgressionOrder)order, Layers = ThreeLayers, PrecinctExponent = SmallPrecincts, BlockExponent = SmallBlocks };
        var options = Ht(layered);

        await Assert.That(RoundTrips(options)).IsTrue();
        await Assert.That(RoundTrips(options with { HtRefinement = true })).IsTrue();
    }

    /// <summary>Tiles, split tile-parts, packet markers and packed headers decode exactly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TiledStreamsRoundTrip()
    {
        var tiled = new JpxTestOptions { Components = Rgb, TileWidth = TileWidth, TileHeight = TileHeight, Markers = true, PackedHeaders = true, SplitTileParts = true };
        var options = Ht(tiled with { HtRefinement = true, Wrap = true });

        await Assert.That(RoundTrips(options)).IsTrue();
    }

    /// <summary>The CAP marker and the Rsiz bit are read and declare Part 15.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CapabilitiesDeclareHighThroughput()
    {
        var ht = JpxCodestream.Read(JpxTestEncoder.Encode(Ht(new()), JpxDecoderTests.Planes(new())));
        var plain = JpxCodestream.Read(JpxTestEncoder.Encode(new(), JpxDecoderTests.Planes(new())));

        await Assert.That(ht!.Capabilities.DeclaresHighThroughput).IsTrue();
        await Assert.That(ht.Capabilities.DeclaresMixed).IsFalse();
        await Assert.That(plain!.Capabilities.DeclaresHighThroughput).IsFalse();
    }

    /// <summary>A Ccap15 value with both mix bits set declares the MIXED mode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CapabilitiesReadTheMixedMode()
    {
        byte[] segment = [0, 0x02, 0, 0, MixedHigh, 0];
        var capabilities = JpxCapabilities.Read(CapRsiz, segment);

        await Assert.That(capabilities.DeclaresMixed).IsTrue();
        await Assert.That(JpxCapabilities.Read(CapRsiz, segment.AsSpan(0, PcapBytes)).HighThroughput).IsEqualTo(0);
    }

    /// <summary>Corrupted and truncated HT data decodes without exceptions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedDataDoesNotThrow()
    {
        var options = Ht(new() { Components = Rgb, HtRefinement = true });
        var data = JpxTestEncoder.Encode(options, JpxDecoderTests.Planes(options));
        var random = new JpegTestRandom(Seed);
        var survived = 0;
        for (var trial = 0; trial < SampleRange; trial++)
        {
            var copy = (byte[])data.Clone();
            var position = random.Next(data.Length / Halved, data.Length);
            copy[position] = (byte)random.Next(0, SampleRange);
            survived += JpxDecoderTests.DecodePlanes(copy) is null ? 0 : 1;
            survived += JpxDecoderTests.DecodePlanes(copy.AsSpan(0, position).ToArray()) is null ? 0 : 1;
        }

        await Assert.That(survived).IsGreaterThan(0);
    }

    /// <summary>Adds the HT block style.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The options with HT code-blocks.</returns>
    private static JpxTestOptions Ht(JpxTestOptions options) => options with { Style = options.Style | JpxBlockStyle.HighThroughput };

    /// <summary>Makes full-range noise for each component.</summary>
    /// <param name="options">The options, with no subsampling.</param>
    /// <returns>The planes.</returns>
    private static int[][] Noise(JpxTestOptions options)
    {
        var random = new JpegTestRandom(Seed);
        var planes = new int[options.Components][];
        for (var c = 0; c < planes.Length; c++)
        {
            planes[c] = new int[options.Width * options.Height];
            for (var i = 0; i < planes[c].Length; i++)
            {
                planes[c][i] = random.Next(0, SampleRange);
            }
        }

        return planes;
    }

    /// <summary>Encodes generated samples and checks the decoder gives them back exactly.</summary>
    /// <param name="options">The options.</param>
    /// <returns><see langword="true"/> when every sample matches.</returns>
    private static bool RoundTrips(JpxTestOptions options) => RoundTrips(options, JpxDecoderTests.Planes(options));

    /// <summary>Encodes samples and checks the decoder gives them back exactly.</summary>
    /// <param name="options">The options.</param>
    /// <param name="planes">The samples.</param>
    /// <returns><see langword="true"/> when every sample matches.</returns>
    private static bool RoundTrips(JpxTestOptions options, int[][] planes)
    {
        var decoded = JpxDecoderTests.DecodePlanes(JpxTestEncoder.Encode(options, planes));
        if (decoded is null || decoded.Length != planes.Length)
        {
            return false;
        }

        for (var c = 0; c < planes.Length; c++)
        {
            if (!decoded[c].AsSpan().SequenceEqual(planes[c]))
            {
                return false;
            }
        }

        return true;
    }
}
