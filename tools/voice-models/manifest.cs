#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --file manifest.cs -- <release folder>");
    return 1;
}

var folder = Path.GetFullPath(args[0]);

using var output = new MemoryStream();

var paths = Directory.GetFiles(folder);

Array.Sort(paths, StringComparer.Ordinal);

using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true }))
{
    writer.WriteStartObject();
    writer.WriteStartArray("files");
    foreach (var path in paths)
    {
        var name = Path.GetFileName(path);
        if (name == "voices.json")
        {
            continue;
        }

        using var input = File.OpenRead(path);
        var digest = await SHA256.HashDataAsync(input).ConfigureAwait(false);
        writer.WriteStartObject();
        writer.WriteString(nameof(name), name);
        writer.WriteNumber("bytes", input.Length);
        writer.WriteString("sha256", Convert.ToHexStringLower(digest));
        writer.WriteEndObject();
    }

    writer.WriteEndArray();
    writer.WriteEndObject();
}

await File.WriteAllBytesAsync(Path.Combine(folder, "voices.json"), output.ToArray()).ConfigureAwait(false);

Console.WriteLine(System.Text.Encoding.UTF8.GetString(output.GetBuffer(), 0, checked((int)output.Length)));

return 0;
