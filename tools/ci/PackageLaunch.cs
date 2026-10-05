// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Checks that an installed viewer can open a PDF and remain running.</summary>
internal static class PackageLaunch
{
    /// <summary>The startup observation period.</summary>
    private static readonly TimeSpan Observation = TimeSpan.FromSeconds(10);

    /// <summary>Opens the check PDF and requires the viewer to remain running.</summary>
    /// <param name="executable">The installed executable.</param>
    /// <param name="arguments">The executable arguments.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The viewer fails to start or exits during the check.</exception>
    internal static async Task CheckAsync(string executable, params string[] arguments)
    {
        Console.WriteLine($"[command]{executable} {string.Join(' ', arguments)}");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {executable}.");
        var output = CopyAsync(process.StandardOutput, Console.Out);
        var error = CopyAsync(process.StandardError, Console.Error);
        try
        {
            await Task.Delay(Observation).ConfigureAwait(false);
            if (process.HasExited)
            {
                throw new InvalidOperationException($"{executable} exited during startup with {await process.WaitForExitStatusAsync().ConfigureAwait(false)}.");
            }

            if (OperatingSystem.IsWindows() && process.MainWindowHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"{executable} did not open a viewer window.");
            }

            Console.WriteLine($"Installed viewer launch passed: {executable}.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync().ConfigureAwait(false);
            await output.ConfigureAwait(false);
            await error.ConfigureAwait(false);
        }
    }

    /// <summary>Streams a child process pipe while it runs.</summary>
    /// <param name="source">The child pipe.</param>
    /// <param name="destination">The workflow log.</param>
    /// <returns>A task.</returns>
    private static async Task CopyAsync(StreamReader source, TextWriter destination)
    {
        while (await source.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            await destination.WriteLineAsync(line).ConfigureAwait(false);
        }
    }
}
