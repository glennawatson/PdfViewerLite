// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.Tracing;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Running;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace PdfViewerLite.Benchmarks;

/// <summary>Runs the benchmarks.</summary>
public static class Program
{
    /// <summary>The switch that records an allocation trace for every benchmark.</summary>
    private const string AuditSwitch = "--audit";

    /// <summary>
    /// Entry point; pass BenchmarkDotNet arguments such as <c>--filter *</c>. With <c>--audit</c> every benchmark also
    /// writes an EventPipe trace sampling each allocation with its stack, which <c>tools/PdfViewerLite.AllocationAudit</c>
    /// reads.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public static void Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var audit = Array.IndexOf(args, AuditSwitch) >= 0;
        var config = audit ? CreateAuditConfig() : null;
        _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run([.. args.Where(static arg => arg != AuditSwitch)], config);
    }

    /// <summary>Creates a configuration recording allocations with stacks.</summary>
    /// <returns>The configuration.</returns>
    private static ManualConfig CreateAuditConfig()
    {
        const ClrTraceEventParser.Keywords keywords = ClrTraceEventParser.Keywords.GC | ClrTraceEventParser.Keywords.Type
            | ClrTraceEventParser.Keywords.GCSampledObjectAllocationHigh | ClrTraceEventParser.Keywords.Stack;
        EventPipeProvider[] providers = [new(ClrTraceEventParser.ProviderName, EventLevel.Verbose, (long)keywords)];
        return DefaultConfig.Instance.AddDiagnoser(new EventPipeProfiler(EventPipeProfile.GcVerbose, providers));
    }
}
