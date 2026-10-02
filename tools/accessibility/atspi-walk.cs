#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

if (args.Length != 3 || !int.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var minimum))
{
    Console.Error.WriteLine("Usage: dotnet run --file atspi-walk.cs -- <a11y bus address> <application bus name> <minimum controls>");
    return 1;
}

const string accessible = "org.a11y.atspi.Accessible";

HashSet<string> controls =
[
    with(comparer: StringComparer.Ordinal),
    "push button", "toggle button", "combo box", "entry", "text", "check box", "radio button", "slider", "page tab", "menu item",
];

HashSet<string> seen = [with(comparer: StringComparer.Ordinal)];

List<string> frames = [];

List<string> bad = [];

var named = 0;

Stack<string> pending = new();

pending.Push("/org/a11y/atspi/accessible/root");

while (pending.TryPop(out var path))
{
    if (!seen.Add(path))
    {
        continue;
    }

    using var roleReply = await CallAsync("call", path, accessible, "GetRoleName").ConfigureAwait(false);
    using var nameReply = await CallAsync("get-property", path, accessible, "Name").ConfigureAwait(false);
    var role = roleReply.RootElement.GetProperty("data")[0].GetString() ?? string.Empty;
    var spoken = (nameReply.RootElement.GetProperty("data").GetString() ?? string.Empty).Trim();
    if (role == "frame")
    {
        frames.Add(spoken);
    }
    else if (controls.Contains(role))
    {
        if (spoken.Length == 0 || spoken.StartsWith("Avalonia.", StringComparison.Ordinal))
        {
            bad.Add($"{role}: '{spoken}'");
        }
        else
        {
            named++;
        }
    }

    using var childrenReply = await CallAsync("call", path, accessible, "GetChildren").ConfigureAwait(false);
    var children = childrenReply.RootElement.GetProperty("data")[0];
    for (var index = children.GetArrayLength() - 1; index >= 0; index--)
    {
        pending.Push(children[index][1].GetString() ?? throw new InvalidDataException("An accessible child has no object path."));
    }
}

Console.WriteLine($"windows: [{string.Join(", ", frames)}]");

Console.WriteLine($"{named} named controls");

foreach (var line in bad)
{
    Console.WriteLine($"needs a name -> {line}");
}

return frames.Count == 0 || named < minimum || bad.Count != 0 ? 1 : 0;

// Calls busctl with separate arguments so bus names and addresses cannot become shell commands.
async Task<JsonDocument> CallAsync(string command, string path, string contract, string member)
{
    var start = new ProcessStartInfo("busctl") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    start.ArgumentList.Add($"--address={args[0]}");
    start.ArgumentList.Add("--json=short");
    start.ArgumentList.Add("--timeout=3");
    start.ArgumentList.Add(command);
    start.ArgumentList.Add(args[1]);
    start.ArgumentList.Add(path);
    start.ArgumentList.Add(contract);
    start.ArgumentList.Add(member);

    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start busctl.");
    var outputTask = process.StandardOutput.ReadToEndAsync();
    var errorTask = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync().ConfigureAwait(false);
    var output = await outputTask.ConfigureAwait(false);
    var error = await errorTask.ConfigureAwait(false);
    if (string.IsNullOrWhiteSpace(output) || !string.IsNullOrWhiteSpace(error))
    {
        throw new InvalidOperationException($"busctl {member} failed: {error}");
    }

    return JsonDocument.Parse(output);
}
