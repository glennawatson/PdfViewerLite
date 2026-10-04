// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Json;

namespace PdfViewerLite.AllocationAudit;

/// <summary>
/// Usage: <c>allocation-audit &lt;trace file or directory&gt; [explained.json]</c>. Prints every PdfViewerLite allocation
/// made inside a measured benchmark workload and exits with 1 when any is not listed in the explained file.
/// </summary>
public static class Program
{
    /// <summary>The exit code when every allocation is explained.</summary>
    private const int Success = 0;

    /// <summary>The exit code when an allocation is unexplained.</summary>
    private const int Unexplained = 1;

    /// <summary>The exit code for bad arguments.</summary>
    private const int Usage = 2;

    /// <summary>Entry point.</summary>
    /// <param name="args">The trace path and optional explained allocations file.</param>
    /// <returns>The exit code.</returns>
    public static int Main(string[] args)
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
        var sites = TraceAuditor.Audit(trace, explained);
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
}
