// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>Reads a tagged document's structure tree into blocks in logical order.</summary>
public sealed partial class PdfiumDocument : ITaggedStructureSource
{
    /// <summary>The least share of the page's marked text the tags must cover to be trusted over the layout.</summary>
    private const double MinimumCoverage = 0.6;

    /// <summary>The least share of the page's text that must be marked content for the tags to be used.</summary>
    private const double MinimumMarked = 0.5;

    /// <summary>The longest element type read, in bytes of UTF-16; standard types are a few characters.</summary>
    private const int TypeBytes = 128;

    /// <summary>The deepest structure nesting followed, guarding against loops in damaged files.</summary>
    private const int MaxDepth = 64;

    /// <summary>Whether the document is tagged: -1 not yet asked, 0 no, 1 yes.</summary>
    private int _tagged = -1;

    /// <inheritdoc/>
    public bool GetTaggedBlocks(int pageIndex, List<TaggedBlock> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        using var scope = PdfiumLibrary.EnterScope();
        if (IsDisposed || !IsTagged())
        {
            return false;
        }

        var page = GetPage(pageIndex);
        var textPage = page?.TextPage;
        if (page is null || textPage is null)
        {
            return false;
        }

        var marked = MapMarkedContent(textPage, out var markedCount, out var visibleCount);
        if (markedCount == 0 || markedCount < visibleCount * MinimumMarked)
        {
            return false;
        }

        return ReadTree(page, marked, markedCount, output);
    }

    /// <summary>Walks a page's structure tree into blocks. Callers hold the PDFium lock.</summary>
    /// <param name="page">The page.</param>
    /// <param name="marked">Each marked content id's characters.</param>
    /// <param name="markedCount">How many visible characters are in marked content.</param>
    /// <param name="output">Receives the blocks.</param>
    /// <returns><see langword="true"/> when the tags cover enough of the page's text.</returns>
    private static bool ReadTree(PdfiumPage page, Dictionary<int, List<int>> marked, int markedCount, List<TaggedBlock> output)
    {
        var tree = NativeMethods.FPDF_StructTree_GetForPage(page.Handle);
        if (tree == 0)
        {
            return false;
        }

        try
        {
            var walker = new StructureWalker(marked);
            var count = NativeMethods.FPDF_StructTree_CountChildren(tree);
            for (var i = 0; i < count; i++)
            {
                walker.Visit(NativeMethods.FPDF_StructTree_GetChildAtIndex(tree, i), 0);
            }

            if (walker.Covered < markedCount * MinimumCoverage || walker.Blocks.Count == 0)
            {
                return false;
            }

            output.AddRange(walker.Blocks);
            return true;
        }
        finally
        {
            NativeMethods.FPDF_StructTree_Close(tree);
        }
    }

    /// <summary>Groups a page's characters by the marked content they are drawn in. Callers hold the PDFium lock.</summary>
    /// <param name="textPage">The text page.</param>
    /// <param name="markedCount">Receives how many visible characters are in marked content.</param>
    /// <param name="visibleCount">Receives how many characters are visible (not spaces and not generated).</param>
    /// <returns>Each marked content id's characters, in drawing order.</returns>
    private static Dictionary<int, List<int>> MapMarkedContent(PdfiumTextPageHandle textPage, out int markedCount, out int visibleCount)
    {
        var map = new Dictionary<int, List<int>>();
        markedCount = 0;
        visibleCount = 0;
        var count = NativeMethods.FPDFText_CountChars(textPage);
        for (var i = 0; i < count; i++)
        {
            var textObject = NativeMethods.FPDFText_GetTextObject(textPage, i);
            if (textObject == 0)
            {
                continue;
            }

            var visible = !char.IsWhiteSpace((char)NativeMethods.FPDFText_GetUnicode(textPage, i));
            visibleCount += visible ? 1 : 0;
            var id = NativeMethods.FPDFPageObj_GetMarkedContentID(textObject);
            if (id < 0)
            {
                continue;
            }

            markedCount += visible ? 1 : 0;
            ref var characters = ref CollectionsMarshal.GetValueRefOrAddDefault(map, id, out _);
            characters ??= [];
            characters.Add(i);
        }

        return map;
    }

    /// <summary>Reads a structure element's type, after role mapping, and works out its role without allocating.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The role.</returns>
    private static unsafe StructureRole ReadRole(nint element)
    {
        Span<byte> buffer = stackalloc byte[TypeBytes];
        int length;
        fixed (byte* bytes = buffer)
        {
            length = (int)Math.Min(NativeMethods.FPDF_StructElement_GetType(element, bytes, new(TypeBytes)).Value, TypeBytes);
        }

        return length <= sizeof(char)
            ? StructureRoles.Classify([])
            : StructureRoles.Classify(MemoryMarshal.Cast<byte, char>(buffer[..(length - sizeof(char))]));
    }

    /// <summary>Reads a structure element string through PDFium's two-call API, allocating only when there is one.</summary>
    /// <param name="element">The element.</param>
    /// <param name="read">The PDFium function.</param>
    /// <returns>The string, or <see langword="null"/> when the element has none.</returns>
    private static unsafe string? ReadElementString(nint element, delegate*<nint, void*, CULong, CULong> read)
    {
        var length = (int)read(element, null, default).Value;
        return length <= sizeof(char) ? null : ReadUtf16(length, (buffer, size) => read(element, buffer, size));
    }

    /// <summary>Determines whether the document is tagged, asking PDFium once. Callers hold the PDFium lock.</summary>
    /// <returns><see langword="true"/> for a tagged document.</returns>
    private bool IsTagged()
    {
        if (_tagged < 0)
        {
            _tagged = NativeMethods.FPDFCatalog_IsTagged(_handle) != 0 ? 1 : 0;
        }

        return _tagged == 1;
    }

    /// <summary>Walks a page's structure tree, turning each block-level element into a <see cref="TaggedBlock"/>.</summary>
    /// <param name="marked">Each marked content id's characters.</param>
    private sealed class StructureWalker(Dictionary<int, List<int>> marked)
    {
        /// <summary>The marked content ids already given to a block, so shared content is read once.</summary>
        private readonly HashSet<int> _used = [];

        /// <summary>Gets the blocks found, in logical order.</summary>
        internal List<TaggedBlock> Blocks { get; } = [];

        /// <summary>Gets how many visible characters the blocks cover.</summary>
        internal int Covered { get; private set; }

        /// <summary>Visits an element: a block-level one becomes a block; a grouping one is walked into.</summary>
        /// <param name="element">The element.</param>
        /// <param name="depth">How deep it is.</param>
        internal void Visit(nint element, int depth)
        {
            if (element == 0 || depth > MaxDepth)
            {
                return;
            }

            var role = ReadRole(element);
            if (role.IsGrouping || (role.IsUnknown && HasElementChildren(element)))
            {
                var count = NativeMethods.FPDF_StructElement_CountChildren(element);
                for (var i = 0; i < count; i++)
                {
                    Visit(NativeMethods.FPDF_StructElement_GetChildAtIndex(element, i), depth + 1);
                }

                return;
            }

            AddBlock(element, role);
        }

        /// <summary>Determines whether an element has child elements rather than only marked content.</summary>
        /// <param name="element">The element.</param>
        /// <returns><see langword="true"/> when it has.</returns>
        private static bool HasElementChildren(nint element)
        {
            var count = NativeMethods.FPDF_StructElement_CountChildren(element);
            for (var i = 0; i < count; i++)
            {
                if (NativeMethods.FPDF_StructElement_GetChildAtIndex(element, i) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Makes a block from an element and everything inside it.</summary>
        /// <param name="element">The element.</param>
        /// <param name="role">What it is.</param>
        private unsafe void AddBlock(nint element, StructureRole role)
        {
            var characters = new List<int>();
            Collect(element, characters, 0);
            var replacement = ReadElementString(element, &NativeMethods.FPDF_StructElement_GetActualText);
            if (role.Kind == ReadingBlockKind.Figure && replacement is null)
            {
                replacement = ReadElementString(element, &NativeMethods.FPDF_StructElement_GetAltText);
            }

            if (characters.Count == 0 && replacement is null)
            {
                return;
            }

            Covered += characters.Count;
            Blocks.Add(new(role.Kind, role.Level, [.. characters], replacement));
        }

        /// <summary>Gathers the characters of an element's marked content and of its descendants, in order.</summary>
        /// <param name="element">The element.</param>
        /// <param name="characters">Receives the character indices.</param>
        /// <param name="depth">How deep it is.</param>
        private void Collect(nint element, List<int> characters, int depth)
        {
            if (depth > MaxDepth)
            {
                return;
            }

            var count = NativeMethods.FPDF_StructElement_CountChildren(element);
            for (var i = 0; i < count; i++)
            {
                var child = NativeMethods.FPDF_StructElement_GetChildAtIndex(element, i);
                if (child != 0)
                {
                    Collect(child, characters, depth + 1);
                    continue;
                }

                Take(NativeMethods.FPDF_StructElement_GetChildMarkedContentID(element, i), characters);
            }

            var direct = NativeMethods.FPDF_StructElement_GetMarkedContentIdCount(element);
            for (var i = 0; i < direct; i++)
            {
                Take(NativeMethods.FPDF_StructElement_GetMarkedContentIdAtIndex(element, i), characters);
            }
        }

        /// <summary>Adds a marked content id's characters, once.</summary>
        /// <param name="id">The id.</param>
        /// <param name="characters">Receives the character indices.</param>
        private void Take(int id, List<int> characters)
        {
            if (id >= 0 && _used.Add(id) && marked.TryGetValue(id, out var drawn))
            {
                characters.AddRange(drawn);
            }
        }
    }
}
