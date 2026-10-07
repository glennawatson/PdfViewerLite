#!/usr/bin/env -S dotnet run --file
#:package Microsoft.Diagnostics.Tracing.TraceEvent
#:property TargetFrameworks=net10.0
#:property TargetFramework=net10.0
#:property IsAotCompatible=false
#:property PublishAot=false

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Reads the EventPipe .nettrace files the benchmarks write and fails on any PdfViewerLite allocation made inside a
// measured workload that benchmarks/allocations-explained.json does not explain. scripts/audit-allocations.sh runs it.
// Usage: dotnet run --file scripts/AllocationAudit.cs -- <trace file or directory> [explained.json]
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

return PdfViewerLite.Scripts.AllocationAudit.Run(args);

namespace PdfViewerLite.Scripts
{
    /// <summary>
    /// Attributes the sampled allocations in EventPipe traces to PdfViewerLite methods. Only allocations made while
    /// BenchmarkDotNet runs the measured workload count; setup and the harness are ignored.
    /// </summary>
    internal static partial class AllocationAudit
    {
        /// <summary>The exit code when every allocation is explained.</summary>
        private const int Success = 0;

        /// <summary>The exit code when an allocation is unexplained.</summary>
        private const int Unexplained = 1;

        /// <summary>The exit code for bad arguments.</summary>
        private const int Usage = 2;

        /// <summary>The namespace prefix of the code under audit.</summary>
        private const string LibraryPrefix = "PdfViewerLite.";

        /// <summary>The namespace prefix of the benchmark classes, which are not under audit.</summary>
        private const string BenchmarkPrefix = "PdfViewerLite.Benchmarks.";

        /// <summary>A frame name BenchmarkDotNet uses for the measured workload loop.</summary>
        private const string WorkloadMarker = "WorkloadAction";

        /// <summary>Entry point.</summary>
        /// <param name="args">The trace path and optional explained allocations file.</param>
        /// <returns>The exit code.</returns>
        internal static int Run(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            var output = Console.Out;
            if (args.Length == 0)
            {
                output.WriteLine("usage: allocation-audit <trace file or directory> [explained.json]");
                return Usage;
            }

            var explained = args.Length > 1 ? LoadExplained(args[1]) : [];
            var traces = Directory.Exists(args[0]) ? Directory.GetFiles(args[0], "*.nettrace", SearchOption.AllDirectories) : [args[0]];
            var unexplained = 0;
            foreach (var trace in traces)
            {
                unexplained += Report(output, trace, explained);
            }

            output.WriteLine(unexplained == 0 ? "All library allocations are explained." : $"{unexplained} unexplained allocation site(s).");
            return unexplained == 0 ? Success : Unexplained;
        }

        /// <summary>Loads the explained allocations.</summary>
        /// <param name="path">The JSON file.</param>
        /// <returns>The entries.</returns>
        private static List<ExplainedAllocation> LoadExplained(string path)
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize(stream, AuditJsonContext.Default.ListExplainedAllocation) ?? [];
        }

        /// <summary>Prints one trace's allocation sites.</summary>
        /// <param name="output">The report writer.</param>
        /// <param name="trace">The trace file.</param>
        /// <param name="explained">The expected allocations.</param>
        /// <returns>The number of unexplained sites.</returns>
        private static int Report(TextWriter output, string trace, List<ExplainedAllocation> explained)
        {
            var sites = Audit(trace, explained);
            output.WriteLine($"{Path.GetFileName(trace)}: {sites.Count} library allocation site(s)");
            var unexplained = 0;
            foreach (var site in sites)
            {
                var reason = site.Explanation?.Reason ?? "UNEXPLAINED";
                unexplained += site.Explanation is null ? 1 : 0;
                output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {site.Bytes:N0} B, {site.Count:N0}x,  {site.Type}  in {site.Frame}  ({reason})"));
            }

            return unexplained;
        }

        /// <summary>Reads a trace and groups its library allocations by type and allocating method.</summary>
        /// <param name="tracePath">The .nettrace file.</param>
        /// <param name="explained">The expected allocations.</param>
        /// <returns>The allocation sites, largest first.</returns>
        private static List<AllocationSite> Audit(string tracePath, IReadOnlyList<ExplainedAllocation> explained)
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
        private static ExplainedAllocation? Explain(IReadOnlyList<ExplainedAllocation> explained, string type, string frame)
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

        /// <summary>Source generated JSON for the explained allocations file.</summary>
        [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
        [JsonSerializable(typeof(List<ExplainedAllocation>))]
        internal sealed partial class AuditJsonContext : JsonSerializerContext;

        /// <summary>Allocations of one type made by one PdfViewerLite method.</summary>
        /// <param name="Type">The allocated type.</param>
        /// <param name="Frame">The innermost PdfViewerLite method on the stack.</param>
        /// <param name="Count">The number of sampled allocations.</param>
        /// <param name="Bytes">The sampled bytes.</param>
        /// <param name="Explanation">The explanation, or <see langword="null"/> when unexplained.</param>
        [DebuggerDisplay("{Type} x{Count} in {Frame}")]
        internal sealed record AllocationSite(string Type, string Frame, long Count, long Bytes, ExplainedAllocation? Explanation);

        /// <summary>An allocation that is expected, with the reason it is acceptable.</summary>
        /// <param name="Type">The allocated type name, or a prefix of it.</param>
        /// <param name="Frame">The PdfViewerLite method that allocates, or a prefix of it.</param>
        /// <param name="Reason">Why the allocation is acceptable.</param>
        [DebuggerDisplay("{Type} in {Frame}")]
        internal sealed record ExplainedAllocation(string Type, string Frame, string Reason);
    }
}
