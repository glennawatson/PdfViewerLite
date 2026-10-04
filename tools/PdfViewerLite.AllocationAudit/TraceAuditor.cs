// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace PdfViewerLite.AllocationAudit;

/// <summary>
/// Attributes the sampled allocations in an EventPipe trace to PdfViewerLite methods. Only allocations made while
/// BenchmarkDotNet runs the measured workload count; setup and the harness are ignored.
/// </summary>
public static class TraceAuditor
{
    /// <summary>The namespace prefix of the code under audit.</summary>
    private const string LibraryPrefix = "PdfViewerLite.";

    /// <summary>The namespace prefix of the benchmark classes, which are not under audit.</summary>
    private const string BenchmarkPrefix = "PdfViewerLite.Benchmarks.";

    /// <summary>A frame name BenchmarkDotNet uses for the measured workload loop.</summary>
    private const string WorkloadMarker = "WorkloadAction";

    /// <summary>Reads a trace and groups its library allocations by type and allocating method.</summary>
    /// <param name="tracePath">The .nettrace file.</param>
    /// <param name="explained">The expected allocations.</param>
    /// <returns>The allocation sites, largest first.</returns>
    public static List<AllocationSite> Audit(string tracePath, IReadOnlyList<ExplainedAllocation> explained)
    {
        ArgumentNullException.ThrowIfNull(explained);
        var etlx = TraceLog.CreateFromEventPipeDataFile(tracePath);
        using var log = new TraceLog(etlx);
        var typeNames = new Dictionary<ulong, string>();
        var sites = new Dictionary<(string Type, string Frame), (long Count, long Bytes)>();
        foreach (var data in log.Events)
        {
            switch (data)
            {
                case GCBulkTypeTraceData bulk:
                {
                    for (var i = 0; i < bulk.Count; i++)
                    {
                        var value = bulk.Values(i);
                        typeNames[value.TypeID] = value.TypeName;
                    }

                    break;
                }

                case GCSampledObjectAllocationTraceData sample:
                {
                    Record(sites, typeNames.GetValueOrDefault(sample.TypeID, $"type 0x{sample.TypeID:X}"), sample.CallStack(), sample.ObjectCountForTypeSample, sample.TotalSizeForTypeSample);
                    break;
                }

                case GCAllocationTickTraceData tick:
                {
                    Record(sites, tick.TypeName, tick.CallStack(), 1, tick.AllocationAmount64);
                    break;
                }
            }
        }

        var result = new List<AllocationSite>(sites.Count);
        foreach (var ((type, frame), (count, bytes)) in sites)
        {
            result.Add(new(type, frame, count, bytes, Explain(explained, type, frame)));
        }

        result.Sort(static (a, b) => b.Bytes.CompareTo(a.Bytes));
        File.Delete(etlx);
        return result;
    }

    /// <summary>Finds the explanation of an allocation.</summary>
    /// <param name="explained">The expected allocations.</param>
    /// <param name="type">The allocated type.</param>
    /// <param name="frame">The allocating method.</param>
    /// <returns>The explanation, or <see langword="null"/>.</returns>
    public static ExplainedAllocation? Explain(IReadOnlyList<ExplainedAllocation> explained, string type, string frame)
    {
        ArgumentNullException.ThrowIfNull(explained);
        foreach (var entry in explained)
        {
            if (type.StartsWith(entry.Type, StringComparison.Ordinal) && frame.StartsWith(entry.Frame, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>Adds a sample when it was made by library code inside the measured workload.</summary>
    /// <param name="sites">The sites so far.</param>
    /// <param name="type">The allocated type.</param>
    /// <param name="stack">The call stack, innermost first.</param>
    /// <param name="count">The number of objects the sample stands for.</param>
    /// <param name="bytes">The bytes the sample stands for.</param>
    private static void Record(Dictionary<(string Type, string Frame), (long Count, long Bytes)> sites, string type, TraceCallStack? stack, long count, long bytes)
    {
        string? libraryFrame = null;
        var inWorkload = false;
        for (var frame = stack; frame is not null; frame = frame.Caller)
        {
            var name = frame.CodeAddress.FullMethodName ?? string.Empty;
            if (libraryFrame is null && name.StartsWith(LibraryPrefix, StringComparison.Ordinal) && !name.StartsWith(BenchmarkPrefix, StringComparison.Ordinal))
            {
                libraryFrame = name;
            }

            inWorkload |= name.Contains(WorkloadMarker, StringComparison.Ordinal);
        }

        if (libraryFrame is null || !inWorkload)
        {
            return;
        }

        var key = (type, libraryFrame);
        var current = sites.GetValueOrDefault(key);
        sites[key] = (current.Count + count, current.Bytes + bytes);
    }
}
