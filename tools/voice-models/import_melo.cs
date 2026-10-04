#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:project ../../src/PdfViewerLite.Core/PdfViewerLite.Core.csproj
#:include MeloFiles.cs

using PdfViewerLite.Tools.VoiceModels;

const int minArguments = 2;

const int maxArguments = 3;

if (args.Length is < minArguments or > maxArguments)
{
    Console.Error.WriteLine("Usage: dotnet run --file import_melo.cs -- <source manifest> <output folder> [<local source folder>]");
    return 1;
}

var files = MeloFiles.Load(args[0]);

var output = Path.GetFullPath(args[1]);

var staging = Path.Combine(output, $".melo-{Guid.NewGuid():N}");

_ = Directory.CreateDirectory(staging);

try
{
    using var client = new HttpClient();
    foreach (var file in files)
    {
        var target = Path.Combine(staging, file.LocalName);
        if (args.Length == maxArguments)
        {
            File.Copy(Path.Combine(args[2], file.LocalName), target);
        }
        else
        {
            using var response = await client.GetAsync(file.Source, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            _ = response.EnsureSuccessStatusCode();
            using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var destination = File.Create(target);
            await source.CopyToAsync(destination).ConfigureAwait(false);
        }
    }

    await MeloFiles.ValidateAsync(files, staging).ConfigureAwait(false);
    foreach (var file in files)
    {
        File.Move(Path.Combine(staging, file.LocalName), Path.Combine(output, file.LocalName), true);
        Console.WriteLine($"imported {file.LocalName}");
    }
}
finally
{
    Directory.Delete(staging, true);
}

return 0;
