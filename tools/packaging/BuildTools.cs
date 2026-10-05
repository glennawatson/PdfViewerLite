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

    /// <summary>Runs a command, requires success and returns its trimmed standard output.</summary>
    /// <param name="executable">The command.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>The standard output without surrounding white space.</returns>
    /// <exception cref="InvalidOperationException">The command cannot start or fails.</exception>
    internal static async Task<string> CaptureAsync(string executable, params string[] arguments)
    {
        Console.WriteLine($"[command]{executable} {string.Join(' ', arguments)}");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {executable}.");
        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var status = await process.WaitForExitStatusAsync().ConfigureAwait(false);
        if (status.ExitCode != 0)
        {
            throw new InvalidOperationException($"{executable} exited with {status}.");
        }

        return output.Trim();
    }
}
