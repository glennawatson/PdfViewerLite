// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Content;

/// <content>The graphics state operators.</content>
internal sealed partial class ContentInterpreter
{
    /// <summary>The most dash lengths kept.</summary>
    private const int MaxDashEntries = 64;

    /// <summary>The operand index of the third matrix entry.</summary>
    private const int MatrixC = 2;

    /// <summary>The operand index of the fifth matrix entry.</summary>
    private const int MatrixE = 4;

    /// <summary>The operand index of the sixth matrix entry.</summary>
    private const int MatrixF = 5;

    /// <summary>The operand index of the fourth matrix entry.</summary>
    private const int MatrixD = 3;

    /// <summary>Handles <c>q</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpSave(ContentInterpreter self, ref ContentReader reader) => self.SaveState();

    /// <summary>Handles <c>Q</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpRestore(ContentInterpreter self, ref ContentReader reader)
    {
        if (self._stack.Count > self._stackFloor)
        {
            self.RestoreState();
        }
    }

    /// <summary>Handles <c>cm</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpConcatMatrix(ContentInterpreter self, ref ContentReader reader)
    {
        var matrix = new Matrix3x2(reader.Number(0), reader.Number(1), reader.Number(MatrixC), reader.Number(MatrixD), reader.Number(MatrixE), reader.Number(MatrixF));
        self._state.Ctm = matrix * self._state.Ctm;
    }

    /// <summary>Handles <c>w</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetLineWidth(ContentInterpreter self, ref ContentReader reader) => self._state.LineWidth = Math.Max(0, reader.Number(0));

    /// <summary>Handles <c>J</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetLineCap(ContentInterpreter self, ref ContentReader reader) => self._state.LineCap = (int)reader.Number(0);

    /// <summary>Handles <c>j</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetLineJoin(ContentInterpreter self, ref ContentReader reader) => self._state.LineJoin = (int)reader.Number(0);

    /// <summary>Handles <c>M</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetMiterLimit(ContentInterpreter self, ref ContentReader reader) => self._state.MiterLimit = reader.Number(0);

    /// <summary>Handles <c>d</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetDash(ContentInterpreter self, ref ContentReader reader)
    {
        var array = reader.Operand(0);
        if (array.Kind != ContentOperandKind.Array)
        {
            return;
        }

        self.SetDash(reader.Body(array), reader.Number(1));
    }

    /// <summary>Handles <c>gs</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetGraphicsState(ContentInterpreter self, ref ContentReader reader)
    {
        var name = reader.Operand(0).Name;
        if (self.FindResource(Objects.KnownName.ExtGState, name).AsDictionary() is { } dictionary)
        {
            self.ApplyExtGState(dictionary);
        }
    }

    /// <summary>Saves the graphics state and the device's clip.</summary>
    private void SaveState()
    {
        _stack.Add(_state);
        _device.Save();
    }

    /// <summary>Restores the most recently saved graphics state and device clip.</summary>
    private void RestoreState()
    {
        var last = _stack.Count - 1;
        _state = _stack[last];
        _stack.RemoveAt(last);
        _device.Restore();
    }

    /// <summary>Sets the dash pattern from the bytes of a dash array.</summary>
    /// <param name="body">The bytes between the brackets.</param>
    /// <param name="phase">The dash phase.</param>
    private void SetDash(ReadOnlySpan<byte> body, float phase)
    {
        Span<float> lengths = stackalloc float[MaxDashEntries];
        var count = ReadNumbers(body, lengths);
        var total = 0F;
        for (var i = 0; i < count; i++)
        {
            total += Math.Abs(lengths[i]);
        }

        _state.Dash = count == 0 || total <= 0 ? [] : [.. lengths[..count]];
        _state.DashPhase = phase;
    }
}
