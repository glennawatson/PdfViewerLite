// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Text;

namespace PdfViewerLite.Tools.Signing;

/// <summary>Signs supported file formats with the connected SimplySign token through jsign.</summary>
internal static class JsignSigner
{
    /// <summary>Maximum attempts while the connected token exposes its certificate.</summary>
    private const int MaximumAttempts = 15;

    /// <summary>Seconds between token certificate discovery attempts.</summary>
    private const int RetrySeconds = 4;

    /// <summary>Signs files with jsign and streams command output.</summary>
    /// <param name="paths">The files to sign.</param>
    /// <param name="scratch">The temporary configuration directory.</param>
    /// <returns>A task that completes after signing.</returns>
    /// <exception cref="InvalidOperationException">A required setting is missing or jsign fails.</exception>
    internal static async Task SignAsync(string[] paths, string scratch)
    {
        var jar = Environment.GetEnvironmentVariable("JSIGN_JAR") ?? throw new InvalidOperationException("JSIGN_JAR is required.");
        var module = Environment.GetEnvironmentVariable("SS_PKCS11") ?? throw new InvalidOperationException("SS_PKCS11 is required.");
        var timestamp = Environment.GetEnvironmentVariable("TS_URL") ?? throw new InvalidOperationException("TS_URL is required.");
        var keystore = Path.Combine(scratch, "sunpkcs11.conf");
        await File.WriteAllLinesAsync(keystore, ["name = SimplySign", $"library = {module}", "slotListIndex = 0"]).ConfigureAwait(false);
        foreach (var path in paths)
        {
            string[] arguments = [
            "-jar",
            jar,
            "--storetype",
            "PKCS11",
            "--keystore",
            keystore,
            "--storepass",
            string.Empty,
            "--alg",
            "SHA-256",
            "--tsmode",
            "RFC3161",
            "--tsaurl",
            timestamp,
            "--replace",
            path,
        ];
            await SignFileAsync(arguments).ConfigureAwait(false);
        }
    }

    /// <summary>Signs a file and retries only certificate discovery errors.</summary>
    /// <param name="arguments">The jsign arguments.</param>
    /// <returns>A task that completes after signing.</returns>
    /// <exception cref="InvalidOperationException">jsign fails or never discovers the certificate.</exception>
    private static async Task SignFileAsync(string[] arguments)
    {
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            await Console.Out.WriteLineAsync($"[command]java {string.Join(' ', arguments)}").ConfigureAwait(false);
            var start = new ProcessStartInfo("java", arguments) { RedirectStandardOutput = true, RedirectStandardError = true };
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start jsign.");
            var output = ForwardOutputAsync(process.StandardOutput, Console.Out);
            var error = ForwardOutputAsync(process.StandardError, Console.Error);
            var status = await process.WaitForExitStatusAsync().ConfigureAwait(false);
            var text = await output.ConfigureAwait(false) + await error.ConfigureAwait(false);
            if (status.ExitCode == 0)
            {
                return;
            }

            if (!text.Contains("No certificate found", StringComparison.Ordinal) || attempt == MaximumAttempts)
            {
                throw new InvalidOperationException($"jsign exited with {status.ExitCode}.");
            }

            await Task.Delay(TimeSpan.FromSeconds(RetrySeconds)).ConfigureAwait(false);
        }
    }

    /// <summary>Streams output while retaining the text needed to classify failures.</summary>
    /// <param name="source">The command output.</param>
    /// <param name="destination">The live log writer.</param>
    /// <returns>The captured output.</returns>
    private static async Task<string> ForwardOutputAsync(StreamReader source, TextWriter destination)
    {
        var captured = new StringBuilder();
        while (await source.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            _ = captured.AppendLine(line);
            await destination.WriteLineAsync(line).ConfigureAwait(false);
        }

        return captured.ToString();
    }
}
