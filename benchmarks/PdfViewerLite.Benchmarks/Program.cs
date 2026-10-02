// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Running;

namespace PdfViewerLite.Benchmarks;

/// <summary>Runs the benchmarks.</summary>
public static class Program
{
    /// <summary>Entry point; pass BenchmarkDotNet arguments such as <c>--filter *</c>.</summary>
    /// <param name="args">The arguments.</param>
    public static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
