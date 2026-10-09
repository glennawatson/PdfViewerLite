// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The 5/3 step that updates even samples: <c>x -= (a + b + 2) &gt;&gt; 2</c> (equation F-5).</summary>
internal readonly record struct JpxReversibleLowStep : IJpxLiftStep<int>
{
    /// <summary>The rounding offset.</summary>
    private const int Rounding = 2;

    /// <summary>The shift that divides by four.</summary>
    private const int Shift = 2;

    /// <inheritdoc/>
    public static void Apply(Span<int> target, ReadOnlySpan<int> first, ReadOnlySpan<int> second, float factor)
    {
        var count = target.Length;
        ref var t = ref MemoryMarshal.GetReference(target);
        ref var a = ref MemoryMarshal.GetReference(first[..count]);
        ref var b = ref MemoryMarshal.GetReference(second[..count]);
        var i = 0;
        if (Vector256.IsHardwareAccelerated)
        {
            var rounding = Vector256.Create(Rounding);
            for (; i <= count - Vector256<int>.Count; i += Vector256<int>.Count)
            {
                var sum = Vector256.LoadUnsafe(ref a, (nuint)i) + Vector256.LoadUnsafe(ref b, (nuint)i) + rounding;
                (Vector256.LoadUnsafe(ref t, (nuint)i) - Vector256.ShiftRightArithmetic(sum, Shift)).StoreUnsafe(ref t, (nuint)i);
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            var rounding = Vector128.Create(Rounding);
            for (; i <= count - Vector128<int>.Count; i += Vector128<int>.Count)
            {
                var sum = Vector128.LoadUnsafe(ref a, (nuint)i) + Vector128.LoadUnsafe(ref b, (nuint)i) + rounding;
                (Vector128.LoadUnsafe(ref t, (nuint)i) - Vector128.ShiftRightArithmetic(sum, Shift)).StoreUnsafe(ref t, (nuint)i);
            }
        }

        for (; i < count; i++)
        {
            target[i] -= (first[i] + second[i] + Rounding) >> Shift;
        }
    }
}
