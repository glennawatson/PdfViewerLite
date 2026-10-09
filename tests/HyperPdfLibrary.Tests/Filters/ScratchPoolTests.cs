// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Checks the bounded pool that keeps large decoder buffers for reuse.</summary>
[NotInParallel(nameof(ScratchPoolTests))]
public sealed class ScratchPoolTests
{
    /// <summary>A length that uses the bounded pool: 1 MiB of bytes.</summary>
    private const int Large = 1024 * 1024;

    /// <summary>A length that uses the shared array pool.</summary>
    private const int Small = 1024;

    /// <summary>How many times over the budget the test returns buffers.</summary>
    private const int Overfill = 2;

    /// <summary>Four megabytes, as a budget that fits a few large buffers.</summary>
    private const long Budget = 4L * Large;

    /// <summary>A returned large buffer is handed out again for a request of about the same size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReusesALargeBuffer()
    {
        ScratchPools.Trim();
        var first = ScratchPool<Marker>.Shared.Rent(Large);
        ScratchPool<Marker>.Shared.Return(first);

        var second = ScratchPool<Marker>.Shared.Rent(Large);
        ScratchPool<Marker>.Shared.Return(second);
        ScratchPools.Trim();

        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(second.Length).IsGreaterThanOrEqualTo(Large);
    }

    /// <summary>Buffers kept for reuse count against the budget, and trimming gives the room back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsBuffersWithinTheBudgetAndTrimReleasesThem()
    {
        var before = ScratchPools.Budget;
        try
        {
            ScratchPools.Trim();
            ScratchPools.Budget = Budget;
            var buffers = new List<byte[]>();
            for (var i = 0; i < Budget / Large * Overfill; i++)
            {
                buffers.Add(ScratchPool<byte>.Shared.Rent(Large + (i * Small)));
            }

            foreach (var buffer in buffers)
            {
                ScratchPool<byte>.Shared.Return(buffer);
            }

            var kept = ScratchPools.RetainedBytes;
            ScratchPools.Trim();

            await Assert.That(kept).IsGreaterThan(0);
            await Assert.That(kept).IsLessThanOrEqualTo(Budget);

            // Decoders in other tests may keep buffers at the same moment, but never more than the budget.
            await Assert.That(ScratchPools.RetainedBytes).IsLessThanOrEqualTo(Budget);
        }
        finally
        {
            ScratchPools.Budget = before;
        }
    }

    /// <summary>With no budget nothing is kept, and lowering the budget trims at once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ALowBudgetKeepsNothing()
    {
        var before = ScratchPools.Budget;
        try
        {
            ScratchPools.Trim();
            var buffer = ScratchPool<byte>.Shared.Rent(Large);
            ScratchPool<byte>.Shared.Return(buffer);
            var kept = ScratchPools.RetainedBytes;

            ScratchPools.Budget = 0;
            var afterLowering = ScratchPools.RetainedBytes;
            ScratchPool<byte>.Shared.Return(ScratchPool<byte>.Shared.Rent(Large));

            await Assert.That(kept).IsGreaterThan(0);
            await Assert.That(afterLowering).IsEqualTo(0);
            await Assert.That(ScratchPools.RetainedBytes).IsEqualTo(0);
        }
        finally
        {
            ScratchPools.Budget = before;
            ScratchPools.Trim();
        }
    }

    /// <summary>Small requests come from the shared array pool and are never kept by the bounded pool.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmallBuffersUseTheSharedPool()
    {
        var buffer = ScratchPool<byte>.Shared.Rent(Small + 1);
        ScratchPool<byte>.Shared.Return(buffer);

        // The shared pool hands out power of two sizes; the bounded pool rounds to eighth steps instead.
        await Assert.That(System.Numerics.BitOperations.IsPow2(buffer.Length)).IsTrue();
        await Assert.That(buffer.Length).IsGreaterThan(Small);
    }

    /// <summary>A type used only by these tests, so no other test shares its pool slots.</summary>
    /// <param name="Value">The byte.</param>
    private readonly record struct Marker(byte Value);
}
