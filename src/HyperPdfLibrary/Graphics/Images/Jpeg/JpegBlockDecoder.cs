// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>Entropy-decodes one block of coefficients for each kind of JPEG scan.</summary>
internal static class JpegBlockDecoder
{
    /// <summary>The symbol that skips sixteen zero coefficients.</summary>
    private const int ZeroRun = 0xF0;

    /// <summary>The bits that hold the size of a coefficient in an AC symbol.</summary>
    private const int SizeMask = 0x0F;

    /// <summary>The shift that gives the run of zeros in an AC symbol.</summary>
    private const int RunShift = 4;

    /// <summary>The zeros skipped by a run symbol with no coefficient.</summary>
    private const int LongRun = 16;

    /// <summary>Decodes a block of a sequential scan: the DC difference and every AC coefficient.</summary>
    /// <param name="reader">The bit reader.</param>
    /// <param name="dc">The DC table.</param>
    /// <param name="ac">The AC table.</param>
    /// <param name="predictor">The previous DC value, updated.</param>
    /// <param name="block">Receives the coefficients in storage order.</param>
    internal static void DecodeSequential(ref JpegBitReader reader, JpegHuffmanTable dc, JpegHuffmanTable ac, ref int predictor, Span<short> block)
    {
        predictor += reader.ReceiveExtend(reader.DecodeSymbol(dc));
        block[0] = (short)predictor;
        var zigzag = JpegBlock.Zigzag;
        var k = 1;
        while (k < JpegBlock.Length && !reader.Failed)
        {
            var symbol = reader.DecodeSymbol(ac);
            var size = symbol & SizeMask;
            if (size == 0)
            {
                if (symbol != ZeroRun)
                {
                    break;
                }

                k += LongRun;
                continue;
            }

            k += symbol >> RunShift;
            block[zigzag[k]] = (short)reader.ReceiveExtend(size);
            k++;
        }
    }

    /// <summary>Decodes the DC coefficient of a progressive scan's first pass.</summary>
    /// <param name="reader">The bit reader.</param>
    /// <param name="dc">The DC table.</param>
    /// <param name="predictor">The previous DC value, updated.</param>
    /// <param name="block">Receives the coefficient.</param>
    /// <param name="low">The successive-approximation bit position.</param>
    internal static void DecodeDcFirst(ref JpegBitReader reader, JpegHuffmanTable dc, ref int predictor, Span<short> block, int low)
    {
        predictor += reader.ReceiveExtend(reader.DecodeSymbol(dc));
        block[0] = (short)(predictor << low);
    }

    /// <summary>Decodes a DC refinement bit.</summary>
    /// <param name="reader">The bit reader.</param>
    /// <param name="block">The block whose DC coefficient gains a bit.</param>
    /// <param name="low">The bit position.</param>
    internal static void DecodeDcRefine(ref JpegBitReader reader, Span<short> block, int low)
    {
        if (reader.ReadBit() != 0)
        {
            block[0] |= (short)(1 << low);
        }
    }

    /// <summary>Decodes the AC coefficients of a progressive scan's first pass over a band.</summary>
    /// <param name="reader">The bit reader.</param>
    /// <param name="ac">The AC table.</param>
    /// <param name="block">Receives the coefficients.</param>
    /// <param name="start">The first zigzag index of the band.</param>
    /// <param name="end">The last zigzag index of the band.</param>
    /// <param name="low">The successive-approximation bit position.</param>
    /// <param name="endOfBandRun">The blocks still to be skipped as all zero in this band, updated.</param>
    internal static void DecodeAcFirst(ref JpegBitReader reader, JpegHuffmanTable ac, Span<short> block, int start, int end, int low, ref int endOfBandRun)
    {
        if (endOfBandRun > 0)
        {
            endOfBandRun--;
            return;
        }

        var zigzag = JpegBlock.Zigzag;
        var k = start;
        while (k <= end && !reader.Failed)
        {
            var symbol = reader.DecodeSymbol(ac);
            var size = symbol & SizeMask;
            var run = symbol >> RunShift;
            if (size == 0)
            {
                if (run < SizeMask)
                {
                    endOfBandRun = ReadRunLength(ref reader, run) - 1;
                    return;
                }

                k += LongRun;
                continue;
            }

            k += run;
            block[zigzag[k]] = (short)(reader.ReceiveExtend(size) * (1 << low));
            k++;
        }
    }

    /// <summary>Decodes a refinement pass over a band of AC coefficients.</summary>
    /// <param name="reader">The bit reader.</param>
    /// <param name="ac">The AC table.</param>
    /// <param name="block">The block being refined.</param>
    /// <param name="start">The first zigzag index of the band.</param>
    /// <param name="end">The last zigzag index of the band.</param>
    /// <param name="low">The bit position being added.</param>
    /// <param name="endOfBandRun">The blocks still to be skipped, updated.</param>
    internal static void DecodeAcRefine(ref JpegBitReader reader, JpegHuffmanTable ac, Span<short> block, int start, int end, int low, ref int endOfBandRun)
    {
        var k = start;
        if (endOfBandRun == 0)
        {
            k = RefineUntilEndOfBand(ref reader, ac, block, start, end, low, ref endOfBandRun);
        }

        if (endOfBandRun <= 0)
        {
            return;
        }

        RefineNonZero(ref reader, block, k, end, low);
        endOfBandRun--;
    }

    /// <summary>Reads the run length of an end-of-band symbol: 2 to the power of the run, plus that many extra bits.</summary>
    /// <param name="reader">The bit reader.</param>
    /// <param name="run">The run field of the symbol.</param>
    /// <returns>The number of blocks the band is all zero for, counting this one.</returns>
    private static int ReadRunLength(ref JpegBitReader reader, int run) => (1 << run) + (run == 0 ? 0 : reader.ReadBits(run));

    /// <summary>Walks symbols until an end-of-band run starts or the band is full, placing new coefficients and refining old ones.</summary>
    /// <param name="reader">The bit reader.</param>
    /// <param name="ac">The AC table.</param>
    /// <param name="block">The block being refined.</param>
    /// <param name="start">The first zigzag index of the band.</param>
    /// <param name="end">The last zigzag index of the band.</param>
    /// <param name="low">The bit position being added.</param>
    /// <param name="endOfBandRun">Receives the run when one starts.</param>
    /// <returns>The zigzag index where decoding stopped.</returns>
    private static int RefineUntilEndOfBand(ref JpegBitReader reader, JpegHuffmanTable ac, Span<short> block, int start, int end, int low, ref int endOfBandRun)
    {
        var zigzag = JpegBlock.Zigzag;
        var positive = (short)(1 << low);
        var negative = (short)-positive;
        var k = start;
        while (k <= end && !reader.Failed)
        {
            var symbol = reader.DecodeSymbol(ac);
            var run = symbol >> RunShift;
            var value = 0;
            if ((symbol & SizeMask) != 0)
            {
                value = reader.ReadBit() != 0 ? positive : negative;
            }
            else if (run < SizeMask)
            {
                endOfBandRun = ReadRunLength(ref reader, run);
                return k;
            }

            k = SkipRefining(ref reader, block, k, end, run, low);
            if (value != 0 && k <= end)
            {
                block[zigzag[k]] = (short)value;
            }

            k++;
        }

        return k;
    }

    /// <summary>
    /// Passes over already-nonzero coefficients, refining each, and over <paramref name="run"/> zero ones, stopping at the zero
    /// coefficient that a new value (or a run of sixteen) lands on.
    /// </summary>
    /// <param name="reader">The bit reader.</param>
    /// <param name="block">The block being refined.</param>
    /// <param name="start">The zigzag index to start from.</param>
    /// <param name="end">The last zigzag index of the band.</param>
    /// <param name="run">The zeros to skip.</param>
    /// <param name="low">The bit position being added.</param>
    /// <returns>The zigzag index reached.</returns>
    private static int SkipRefining(ref JpegBitReader reader, Span<short> block, int start, int end, int run, int low)
    {
        var zigzag = JpegBlock.Zigzag;
        var k = start;
        var remaining = run;
        while (k <= end)
        {
            ref var coefficient = ref block[zigzag[k]];
            if (coefficient != 0)
            {
                RefineCoefficient(ref reader, ref coefficient, low);
            }
            else
            {
                remaining--;
                if (remaining < 0)
                {
                    break;
                }
            }

            k++;
        }

        return k;
    }

    /// <summary>Refines every nonzero coefficient from an index to the end of the band.</summary>
    /// <param name="reader">The bit reader.</param>
    /// <param name="block">The block being refined.</param>
    /// <param name="start">The zigzag index to start from.</param>
    /// <param name="end">The last zigzag index of the band.</param>
    /// <param name="low">The bit position being added.</param>
    private static void RefineNonZero(ref JpegBitReader reader, Span<short> block, int start, int end, int low)
    {
        var zigzag = JpegBlock.Zigzag;
        for (var k = start; k <= end; k++)
        {
            ref var coefficient = ref block[zigzag[k]];
            if (coefficient != 0)
            {
                RefineCoefficient(ref reader, ref coefficient, low);
            }
        }
    }

    /// <summary>Adds one correction bit to a nonzero coefficient.</summary>
    /// <param name="reader">The bit reader.</param>
    /// <param name="coefficient">The coefficient.</param>
    /// <param name="low">The bit position being added.</param>
    private static void RefineCoefficient(ref JpegBitReader reader, ref short coefficient, int low)
    {
        var bit = (short)(1 << low);
        if (reader.ReadBit() != 0 && (coefficient & bit) == 0)
        {
            coefficient += coefficient >= 0 ? bit : (short)-bit;
        }
    }
}
