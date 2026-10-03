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
    /// <summary>
    /// Entry point; pass BenchmarkDotNet arguments such as <c>--filter *</c>. Allocations are measured only through
    /// EventPipe: every run writes a .nettrace per benchmark sampling each allocation with its stack, which
    /// <c>tools/PdfViewerLite.AllocationAudit</c> reads and checks against <c>benchmarks/allocations-explained.json</c>.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, CreateConfig());

    /// <summary>Creates the configuration: the default jobs plus an EventPipe allocation trace.</summary>
    /// <returns>The configuration.</returns>
    private static ManualConfig CreateConfig()
    {
        const ClrTraceEventParser.Keywords keywords = ClrTraceEventParser.Keywords.GC | ClrTraceEventParser.Keywords.Type
            | ClrTraceEventParser.Keywords.GCSampledObjectAllocationHigh | ClrTraceEventParser.Keywords.Stack;
        EventPipeProvider[] providers = [new(ClrTraceEventParser.ProviderName, EventLevel.Verbose, (long)keywords)];
        return DefaultConfig.Instance.AddDiagnoser(new EventPipeProfiler(EventPipeProfile.GcVerbose, providers));
    }
}
