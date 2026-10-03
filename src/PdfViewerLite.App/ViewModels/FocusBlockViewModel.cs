// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Reading;
using ReactiveUI;

namespace PdfViewerLite.App.ViewModels;

/// <summary>A block of text in Focus Mode, with the parts being read aloud marked.</summary>
[DebuggerDisplay("{Kind}: {Text}")]
public sealed class FocusBlockViewModel : ReactiveObject
{
    /// <summary>Initializes a new instance of the <see cref="FocusBlockViewModel"/> class.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="block">The block.</param>
    /// <param name="offset">Where the block starts in the page's reading text.</param>
    public FocusBlockViewModel(int pageIndex, ReadingBlock block, int offset)
    {
        ArgumentNullException.ThrowIfNull(block);
        PageIndex = pageIndex;
        Kind = block.Kind;
        Text = block.Text;
        Bounds = block.Bounds;
        FirstCharacter = Array.Find(block.CharIndices, static index => index >= 0);
        Offset = offset;
    }

    /// <summary>Gets the page.</summary>
    public int PageIndex { get; }

    /// <summary>Gets what the block is.</summary>
    public ReadingBlockKind Kind { get; }

    /// <summary>Gets the text.</summary>
    public string Text { get; }

    /// <summary>Gets the block's box on the page, used to keep the place when switching views.</summary>
    public PageRect Bounds { get; }

    /// <summary>Gets the page character the block starts with, for Read from Here.</summary>
    public int FirstCharacter { get; }

    /// <summary>Gets where the block starts in the page's reading text.</summary>
    public int Offset { get; }

    /// <summary>Gets the part of the block being read aloud, relative to the block.</summary>
    public TextRange Spoken
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = TextRange.None;

    /// <summary>Gets the word being read aloud, relative to the block.</summary>
    public TextRange Word
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = TextRange.None;

    /// <summary>Gets a value indicating whether the block is dimmed by the focus band.</summary>
    public bool IsDimmed
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Clips a range of the page's reading text to this block.</summary>
    /// <param name="range">The range.</param>
    /// <param name="offset">Where the block starts.</param>
    /// <param name="length">The block's length.</param>
    /// <returns>The part inside the block, relative to it.</returns>
    public static TextRange Clip(TextRange range, int offset, int length)
    {
        if (range.IsEmpty)
        {
            return TextRange.None;
        }

        var start = Math.Max(range.Start, offset);
        var end = Math.Min(range.End, offset + length);
        return end > start ? new(start - offset, end - start) : TextRange.None;
    }

    /// <summary>Updates the marks from what is being read on the block's page.</summary>
    /// <param name="spoken">The sentence, in the page's reading text, or none.</param>
    /// <param name="word">The word, or none.</param>
    /// <param name="focusBand">Whether blocks away from the sentence are dimmed.</param>
    public void Mark(TextRange spoken, TextRange word, bool focusBand)
    {
        Spoken = Clip(spoken, Offset, Text.Length);
        Word = Clip(word, Offset, Text.Length);
        IsDimmed = focusBand && !spoken.IsEmpty && Spoken.IsEmpty;
    }
}
