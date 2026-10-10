// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Reads content operators and dispatches them to their handlers.</summary>
internal static class PageContentParse
{
    /// <summary>The initial capacity of the object list.</summary>
    internal const int ObjectCapacity = 64;

    /// <summary>The initial capacity of the state stack.</summary>
    internal const int StackCapacity = 16;

    /// <summary>The initial capacity of a path's segment list.</summary>
    internal const int SegmentCapacity = 32;

    /// <summary>How many operators are read between checks of the cancellation token.</summary>
    internal const int CancellationInterval = 1024;

    /// <summary>The operand index of the third operand.</summary>
    internal const int ThirdOperand = 2;

    /// <summary>The operand index of the fourth operand.</summary>
    internal const int FourthOperand = 3;

    /// <summary>The operand index of the fifth operand.</summary>
    internal const int FifthOperand = 4;

    /// <summary>The operand index of the sixth operand.</summary>
    internal const int SixthOperand = 5;

    /// <summary>Reads the whole content.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "cancellationToken">Checked every <see cref = "PageContentParse.CancellationInterval"/> operators.</param>
    /// <returns>The objects in painting order.</returns>
    internal static List<PdfPageObject> Parse(PageContentParseState state, CancellationToken cancellationToken)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(state.Content, state.Names, operands);
        var previousEnd = 0;
        var count = 0;
        while (reader.Next(out var op))
        {
            count++;
            if (count % PageContentParse.CancellationInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            state.OperatorStart = previousEnd;
            state.OperatorEnd = reader.Position;
            previousEnd = state.OperatorEnd;
            PageContentParseDispatch.Dispatch(state, op, ref reader);
        }

        return state.Objects;
    }

    /// <summary>Determines whether an exception from a damaged object should only skip the operator that hit it.</summary>
    /// <param name = "exception">The exception.</param>
    /// <returns><see langword="true"/> when the exception is recoverable.</returns>
    internal static bool IsRecoverable(Exception exception) =>
        exception is InvalidDataException or
        PdfException or
        ArgumentException or
        InvalidOperationException or
        IndexOutOfRangeException or
        NotSupportedException or
        FormatException or
        OverflowException;
}
