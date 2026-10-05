// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.Tools.Packaging;

/// <summary>Runs packaging commands.</summary>
internal static class BuildTools
{
    /// <summary>Runs a command and requires success.</summary>
    /// <param name="executable">The command.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <exception cref="InvalidOperationException">The command fails.</exception>
    internal static void Run(string executable, params IEnumerable<string> arguments)
    {
        string[] values = [.. arguments];

        Console.WriteLine($"[command]{executable} {string.Join(' ', values)}");

        var result = Process.Run(executable, values);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"{executable} exited with {result.ExitCode}.");
        }
    }
}
