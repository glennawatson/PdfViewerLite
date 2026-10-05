// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Inspects named controls through AT-SPI.</summary>
internal static class AtspiWalkCommand
{
    /// <summary>The accessible object interface.</summary>
    private const string Accessible = "org.a11y.atspi.Accessible";

    /// <summary>The roles that need spoken names.</summary>
    private static readonly FrozenSet<string> Controls = FrozenSet.ToFrozenSet(
    [
        "push button", "toggle button", "combo box", "entry", "text", "check box",
        "radio button", "slider", "page tab", "menu item",
    ], StringComparer.Ordinal);

    /// <summary>Runs the accessibility inspection.</summary>
    /// <param name="args">The bus address, application and minimum named control count.</param>
    /// <returns>The command exit code.</returns>
    internal static async Task<int> RunAsync(string[] args)
    {
        const int argumentCount = 3;
        if (args.Length != argumentCount || !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var minimum))
        {
            await Console.Error.WriteLineAsync("Usage: PdfViewerLite.Tools accessibility atspi-walk <address> <application> <minimum-controls>");
            return 1;
        }

        var state = await WalkAsync(args[0], args[1]).ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"windows: [{string.Join(", ", state.Frames)}]");
        await Console.Out.WriteLineAsync($"{state.Named} named controls");
        foreach (var line in state.Bad)
        {
            await Console.Out.WriteLineAsync($"needs a name -> {line}");
        }

        return state.Frames.Count == 0 || state.Named < minimum || state.Bad.Count != 0 ? 1 : 0;
    }

    /// <summary>Visits every accessible object once.</summary>
    /// <param name="address">The accessibility bus address.</param>
    /// <param name="application">The application bus name.</param>
    /// <returns>The inspection results.</returns>
    /// <exception cref="InvalidDataException">A child has no object path.</exception>
    private static async Task<WalkState> WalkAsync(string address, string application)
    {
        var state = new WalkState();
        HashSet<string> seen = [with(comparer: StringComparer.Ordinal)];
        Stack<string> pending = new();
        pending.Push("/org/a11y/atspi/accessible/root");
        while (pending.TryPop(out var path))
        {
            if (!seen.Add(path))
            {
                continue;
            }

            using var roleReply = await CallAsync(address, application, "call", path, "GetRoleName").ConfigureAwait(false);
            using var nameReply = await CallAsync(address, application, "get-property", path, "Name").ConfigureAwait(false);
            var role = roleReply.RootElement.GetProperty("data")[0].GetString() ?? string.Empty;
            var spoken = (nameReply.RootElement.GetProperty("data").GetString() ?? string.Empty).Trim();
            Observe(state, role, spoken);
            using var childrenReply = await CallAsync(address, application, "call", path, "GetChildren").ConfigureAwait(false);
            var children = childrenReply.RootElement.GetProperty("data")[0];
            for (var index = children.GetArrayLength() - 1; index >= 0; index--)
            {
                pending.Push(children[index][1].GetString() ?? throw new InvalidDataException("An accessible child has no object path."));
            }
        }

        return state;
    }

    /// <summary>Records windows and checks spoken control names.</summary>
    /// <param name="state">The inspection results.</param>
    /// <param name="role">The accessible role.</param>
    /// <param name="spoken">The trimmed spoken name.</param>
    private static void Observe(WalkState state, string role, string spoken)
    {
        if (role == "frame")
        {
            state.Frames.Add(spoken);
        }
        else if (Controls.Contains(role))
        {
            if (spoken.Length == 0 || spoken.StartsWith("Avalonia.", StringComparison.Ordinal))
            {
                state.Bad.Add($"{role}: '{spoken}'");
            }
            else
            {
                state.Named++;
            }
        }
    }

    /// <summary>Calls busctl with separately passed arguments.</summary>
    /// <param name="address">The accessibility bus address.</param>
    /// <param name="application">The application bus name.</param>
    /// <param name="command">The busctl operation.</param>
    /// <param name="path">The accessible object path.</param>
    /// <param name="member">The property or method name.</param>
    /// <returns>The reply owned by the caller.</returns>
    /// <exception cref="InvalidOperationException">busctl fails or returns no reply.</exception>
    private static async Task<JsonDocument> CallAsync(string address, string application, string command, string path, string member)
    {
        var start = new ProcessStartInfo("busctl") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add($"--address={address}");
        start.ArgumentList.Add("--json=short");
        start.ArgumentList.Add("--timeout=3");
        start.ArgumentList.Add(command);
        start.ArgumentList.Add(application);
        start.ArgumentList.Add(path);
        start.ArgumentList.Add(Accessible);
        start.ArgumentList.Add(member);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start busctl.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        var status = await process.WaitForExitStatusAsync().ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (status.ExitCode != 0 || string.IsNullOrWhiteSpace(output) || !string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException($"busctl {member} failed: {error}");
        }

        return JsonDocument.Parse(output);
    }

    /// <summary>The accumulated accessibility inspection.</summary>
    private sealed class WalkState
    {
        /// <summary>Gets the spoken window names.</summary>
        internal List<string> Frames { get; } = [];

        /// <summary>Gets controls with invalid names.</summary>
        internal List<string> Bad { get; } = [];

        /// <summary>Gets or sets the count of correctly named controls.</summary>
        internal int Named { get; set; }
    }
}
