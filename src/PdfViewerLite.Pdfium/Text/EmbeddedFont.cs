// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using PdfViewerLite.Core.Text.Fonts;
using PdfViewerLite.Core.Text.Layout;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium.Text;

/// <summary>
/// An installed font embedded in one document, cut down to the glyphs written so far. Glyphs are added a Unicode
/// block at a time, so typing more text in the same script reuses the embedded font; text that needs a glyph outside
/// it embeds a larger subset for the new text, and earlier text keeps the font it was written with. Callers hold the
/// PDFium lock.
/// </summary>
[DebuggerDisplay("EmbeddedFont: {_program.Face.Family}, {_covered.Count} glyphs")]
internal sealed unsafe class EmbeddedFont : IDisposable
{
    /// <summary>The bits of a code point's block: blocks are 128 characters.</summary>
    private const int BlockShift = 7;

    /// <summary>The characters in a block, less one.</summary>
    private const int BlockMask = (1 << BlockShift) - 1;

    /// <summary>The bytes of one CID to glyph map entry.</summary>
    private const int MapEntryBytes = 2;

    /// <summary>The font.</summary>
    private readonly FontProgram _program;

    /// <summary>The document.</summary>
    private readonly PdfiumDocumentHandle _document;

    /// <summary>The glyphs embedded so far, by original glyph number.</summary>
    private readonly HashSet<ushort> _covered = [0];

    /// <summary>The blocks of characters embedded so far.</summary>
    private readonly HashSet<int> _blocks = [];

    /// <summary>The text each original glyph shows.</summary>
    private readonly Dictionary<ushort, string> _texts = [];

    /// <summary>The characters and glyphs of a block, reused.</summary>
    private readonly List<(int CodePoint, ushort Glyph)> _blockGlyphs = [];

    /// <summary>The loaded font, or <see langword="null"/> before the first text.</summary>
    private PdfiumFontHandle? _handle;

    /// <summary>Initializes a new instance of the <see cref="EmbeddedFont"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="program">The font.</param>
    internal EmbeddedFont(PdfiumDocumentHandle document, FontProgram program)
    {
        _document = document;
        _program = program;
    }

    /// <summary>Gets the subset embedded last, which maps original glyphs to the codes written.</summary>
    internal FontSubset? Subset { get; private set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
    }

    /// <summary>Makes sure the embedded font holds every glyph of some laid out text, embedding a larger subset when not.</summary>
    /// <param name="text">The text.</param>
    /// <param name="glyphs">Its glyphs.</param>
    /// <returns>The font to write the text with, or <see langword="null"/> when it cannot be embedded.</returns>
    internal PdfiumFontHandle? Prepare(string text, ReadOnlySpan<LaidGlyph> glyphs)
    {
        var missing = false;
        foreach (var glyph in glyphs)
        {
            missing |= !_covered.Contains(glyph.Glyph);
        }

        if (!missing && _handle is not null)
        {
            return _handle;
        }

        AddBlocks(text);
        AddShaped(text, glyphs);
        return Load();
    }

    /// <summary>Finds where a glyph's characters end: the nearest later cluster, or the end of the text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="glyphs">The glyphs.</param>
    /// <param name="index">The glyph.</param>
    /// <returns>The index after its last character.</returns>
    private static int ClusterEnd(string text, ReadOnlySpan<LaidGlyph> glyphs, int index)
    {
        var cluster = glyphs[index].Cluster;
        var end = text.Length;
        foreach (var other in glyphs)
        {
            if (other.Cluster > cluster && other.Cluster < end)
            {
                end = other.Cluster;
            }
        }

        // A line break ends the text a glyph shows.
        var newline = text.AsSpan(cluster, end - cluster).IndexOfAny('\r', '\n');
        return newline < 0 ? end : cluster + newline;
    }

    /// <summary>Adds every glyph of each Unicode block the text uses, so more text in the same script needs no new subset.</summary>
    /// <param name="text">The text.</param>
    private void AddBlocks(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            var block = rune.Value >> BlockShift;
            if (!_blocks.Add(block))
            {
                continue;
            }

            _blockGlyphs.Clear();
            _program.MapCharacters(block << BlockShift, (block << BlockShift) | BlockMask, _blockGlyphs);
            foreach (var (codePoint, glyph) in _blockGlyphs)
            {
                _ = _covered.Add(glyph);
                _ = _texts.TryAdd(glyph, char.ConvertFromUtf32(codePoint));
            }
        }
    }

    /// <summary>Adds the shaped glyphs, such as ligatures, with the text each shows.</summary>
    /// <param name="text">The text.</param>
    /// <param name="glyphs">The glyphs.</param>
    private void AddShaped(string text, ReadOnlySpan<LaidGlyph> glyphs)
    {
        for (var i = 0; i < glyphs.Length; i++)
        {
            var glyph = glyphs[i];
            _ = _covered.Add(glyph.Glyph);
            if (_texts.ContainsKey(glyph.Glyph) || (i > 0 && glyphs[i - 1].Cluster == glyph.Cluster))
            {
                continue;
            }

            var end = ClusterEnd(text, glyphs, i);
            if (end > glyph.Cluster)
            {
                _texts[glyph.Glyph] = text[glyph.Cluster..end];
            }
        }
    }

    /// <summary>Embeds a subset of the covered glyphs with its text map.</summary>
    /// <returns>The font, or <see langword="null"/>.</returns>
    private PdfiumFontHandle? Load()
    {
        if (FontSubsetter.Create(_program, _covered) is not { } subset)
        {
            return null;
        }

        var entries = new List<(ushort Code, string Text)>(subset.GlyphCount);
        for (var code = 0; code < subset.GlyphCount; code++)
        {
            entries.Add(((ushort)code, _texts.GetValueOrDefault(subset.Original[code], string.Empty)));
        }

        var cmap = Encoding.ASCII.GetBytes($"{ToUnicodeCMap.Write(entries)}\0");
        var map = new byte[subset.GlyphCount * MapEntryBytes];
        for (var code = 0; code < subset.GlyphCount; code++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(map.AsSpan(code * MapEntryBytes), (ushort)code);
        }

        PdfiumFontHandle handle;
        fixed (byte* data = subset.Data)
        {
            fixed (byte* toUnicode = cmap)
            {
                fixed (byte* glyphMap = map)
                {
                    handle = NativeMethods.FPDFText_LoadCidType2Font(_document, data, (uint)subset.Data.Length, toUnicode, glyphMap, (uint)map.Length);
                }
            }
        }

        if (handle.IsInvalid)
        {
            handle.Dispose();
            return null;
        }

        // Text written earlier holds its own reference to the font it was written with.
        _handle?.Dispose();
        _handle = handle;
        Subset = subset;
        return handle;
    }
}
