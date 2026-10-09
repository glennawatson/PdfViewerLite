// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <content>The symbol dictionary, pattern dictionary and table segments.</content>
internal sealed partial class Jbig2Context
{
    /// <summary>The symbol dictionary flag for refinement and aggregation.</summary>
    private const int RefinementAggregateFlag = 0x0002;

    /// <summary>The shift of the generic template in symbol dictionary flags.</summary>
    private const int DictionaryTemplateShift = 10;

    /// <summary>The shift of the refinement template in symbol dictionary flags.</summary>
    private const int DictionaryRefinementShift = 12;

    /// <summary>The symbol dictionary flag that reuses the last referred dictionary's contexts.</summary>
    private const int ReuseContextsFlag = 0x0100;

    /// <summary>The symbol dictionary flag that keeps the contexts for a later dictionary.</summary>
    private const int RetainContextsFlag = 0x0200;

    /// <summary>Determines whether a dictionary retained contexts of the sizes a reusing dictionary needs, as PDFium checks.</summary>
    /// <param name="last">The last referred dictionary.</param>
    /// <param name="genericLength">The generic contexts needed, or zero.</param>
    /// <param name="refinementLength">The refinement contexts needed, or zero.</param>
    /// <returns><see langword="true"/> when the sizes match.</returns>
    private static bool ContextsMatch(Jbig2SymbolDictionary last, int genericLength, int refinementLength) =>
        (genericLength == 0 || last.GenericContexts.Length == genericLength)
        && (refinementLength == 0 || last.RefinementContexts.Length == refinementLength);

    /// <summary>Gets the generic contexts a reused dictionary passes on.</summary>
    /// <param name="reused">The dictionary whose contexts are reused, or <see langword="null"/>.</param>
    /// <returns>The contexts; empty when none are reused.</returns>
    private static ReadOnlySpan<byte> ReusedGeneric(Jbig2SymbolDictionary? reused) => reused is null ? default : reused.GenericContexts;

    /// <summary>Gets the refinement contexts a reused dictionary passes on.</summary>
    /// <param name="reused">The dictionary whose contexts are reused, or <see langword="null"/>.</param>
    /// <returns>The contexts; empty when none are reused.</returns>
    private static ReadOnlySpan<byte> ReusedRefinement(Jbig2SymbolDictionary? reused) => reused is null ? default : reused.RefinementContexts;

    /// <summary>Copies contexts for a later dictionary when the dictionary asks for it.</summary>
    /// <param name="settings">The dictionary parameters.</param>
    /// <param name="contexts">The final contexts.</param>
    /// <returns>The copy, or <see langword="null"/>.</returns>
    private static byte[]? Retain(Jbig2SymbolDictionarySettings settings, ReadOnlySpan<byte> contexts) =>
        settings.RetainContexts && !contexts.IsEmpty ? contexts.ToArray() : null;

    /// <summary>Reads a symbol dictionary's header fields.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="settings">The dictionary parameters.</param>
    /// <param name="flags">The flags.</param>
    /// <returns><see langword="false"/> when the data ends or a count is too large.</returns>
    private static bool TryReadSymbolDictionary(ref Jbig2Reader reader, out Jbig2SymbolDictionarySettings settings, out ushort flags)
    {
        settings = new();
        if (!reader.TryReadUInt16(out flags))
        {
            return false;
        }

        settings.Huffman = (flags & HuffmanFlag) != 0;
        settings.RefinementAggregate = (flags & RefinementAggregateFlag) != 0;
        settings.Template = (flags >> DictionaryTemplateShift) & TwoBits;
        settings.RefinementTemplate = ((flags >> DictionaryRefinementShift) & TwoBits) != 0 ? 1 : 0;
        settings.ReuseContexts = (flags & ReuseContextsFlag) != 0;
        settings.RetainContexts = (flags & RetainContextsFlag) != 0;
        if (!TryReadDictionaryAt(ref reader, settings) || !reader.TryReadUInt32(out var exported) || !reader.TryReadUInt32(out var created))
        {
            return false;
        }

        settings.ExportCount = exported;
        settings.NewCount = (int)Math.Min(created, int.MaxValue);
        return exported <= Jbig2Limits.MaxExportSymbols && created <= Jbig2Limits.MaxNewSymbols;
    }

    /// <summary>Reads the generic and refinement adaptive pixels of a symbol dictionary.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="settings">The dictionary parameters.</param>
    /// <returns><see langword="false"/> when the data ends.</returns>
    private static bool TryReadDictionaryAt(ref Jbig2Reader reader, Jbig2SymbolDictionarySettings settings)
    {
        var at = default(Jbig2AtPixels);
        if (!settings.Huffman && !TryReadAt(ref reader, settings.Template, out at))
        {
            return false;
        }

        settings.At = at;
        if (!settings.RefinementAggregate || settings.RefinementTemplate != 0)
        {
            return true;
        }

        var read = TryReadRefinementAt(ref reader, out var refinementAt);
        settings.RefinementAt = refinementAt;
        return read;
    }

    /// <summary>Processes a table segment (T.88 section 7.4.13).</summary>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <returns>The status.</returns>
    private static Jbig2Status DecodeTable(ref Jbig2Reader reader, Jbig2Segment segment)
    {
        segment.Table = Jbig2HuffmanTable.Parse(ref reader);
        reader.AlignByte();
        return segment.Table is null ? Jbig2Status.Failure : Jbig2Status.Success;
    }

    /// <summary>Processes a symbol dictionary segment (T.88 section 7.4.2).</summary>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <returns>The status.</returns>
    private Jbig2Status DecodeSymbolDictionary(ref Jbig2Reader reader, Jbig2Segment segment)
    {
        if (!TryReadSymbolDictionary(ref reader, out var settings, out var flags) || !AllReferredFound(segment) || !TrySelectDictionaryTables(segment, settings, flags))
        {
            return Jbig2Status.Failure;
        }

        var genericLength = settings.Huffman ? 0 : Jbig2GenericParameters.ContextCountFor(settings.Template);
        var refinementLength = settings.RefinementAggregate ? Jbig2RefinementParameters.ContextCountFor(settings.RefinementTemplate) : 0;
        var reused = settings.ReuseContexts ? LastReferredDictionary(segment) : null;
        if (reused is not null && !ContextsMatch(reused, genericLength, refinementLength))
        {
            return Jbig2Status.Failure;
        }

        using var generic = new Jbig2ContextBuffer(genericLength, ReusedGeneric(reused));
        using var refinement = new Jbig2ContextBuffer(refinementLength, ReusedRefinement(reused));
        if (DecodeSymbols(ref reader, settings, CollectSymbols(segment), generic.Span, refinement.Span) is not { } symbols)
        {
            return Jbig2Status.Failure;
        }

        segment.Symbols = new(symbols, Retain(settings, generic.Span), Retain(settings, refinement.Span));
        return Jbig2Status.Success;
    }

    /// <summary>Decodes a symbol dictionary's symbols with the right coding.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="settings">The dictionary parameters.</param>
    /// <param name="inputs">The input symbols.</param>
    /// <param name="generic">The generic contexts.</param>
    /// <param name="refinement">The refinement contexts.</param>
    /// <returns>The exported symbols, or <see langword="null"/>.</returns>
    private Jbig2SymbolStore? DecodeSymbols(ref Jbig2Reader reader, Jbig2SymbolDictionarySettings settings, Jbig2SymbolSet inputs, Span<byte> generic, Span<byte> refinement)
    {
        if (settings.Huffman)
        {
            var huffman = Jbig2SymbolDictionaryDecoder.DecodeHuffman(ref reader, settings, inputs, refinement, _workspace);
            reader.AlignByte();
            return huffman;
        }

        var decoder = new Jbig2ArithmeticDecoder(reader.Data, reader.Offset);
        var symbols = Jbig2SymbolDictionaryDecoder.DecodeArithmetic(ref decoder, settings, inputs, generic, refinement, _workspace);
        FinishArithmetic(ref reader, decoder.Position);
        return symbols;
    }

    /// <summary>Gets the last symbol dictionary a segment refers to.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns>The dictionary, or <see langword="null"/>.</returns>
    private Jbig2SymbolDictionary? LastReferredDictionary(Jbig2Segment segment)
    {
        Jbig2SymbolDictionary? last = null;
        foreach (var number in segment.Referred)
        {
            if (Find(number) is { Symbols: { } dictionary })
            {
                last = dictionary;
            }
        }

        return last;
    }

    /// <summary>Processes a pattern dictionary segment (T.88 section 7.4.4).</summary>
    /// <param name="reader">The reader, at the segment data.</param>
    /// <param name="segment">The segment.</param>
    /// <returns>The status.</returns>
    private Jbig2Status DecodePatternDictionary(ref Jbig2Reader reader, Jbig2Segment segment)
    {
        if (!reader.TryReadByte(out var flags) || !reader.TryReadByte(out var width) || !reader.TryReadByte(out var height)
            || !reader.TryReadUInt32(out var grayMax) || grayMax > Jbig2Limits.MaxPatternIndex)
        {
            return Jbig2Status.Failure;
        }

        var template = (flags >> TemplateShift) & TemplateMask;
        if ((flags & MmrFlag) != 0)
        {
            segment.Patterns = Jbig2PatternDictionary.DecodeMmr(reader.Data[reader.Offset..], width, height, (int)grayMax, _workspace);
        }
        else
        {
            using var contexts = new Jbig2ContextBuffer(Jbig2GenericParameters.ContextCountFor(template));
            var decoder = new Jbig2ArithmeticDecoder(reader.Data, reader.Offset);
            segment.Patterns = Jbig2PatternDictionary.DecodeArithmetic(ref decoder, contexts.Span, template, width, height, (int)grayMax, _workspace);
            FinishArithmetic(ref reader, decoder.Position);
        }

        return segment.Patterns is null ? Jbig2Status.Failure : Jbig2Status.Success;
    }
}
