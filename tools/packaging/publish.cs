#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:include BuildTools.cs

using PdfViewerLite.Tools.Packaging;

if (args is not [var rid, var version] || rid is not ("linux-x64" or "linux-arm64" or "win-x64" or "win-arm64" or "osx-x64" or "osx-arm64"))
{
    Console.Error.WriteLine("Usage: dotnet run --file publish.cs -- <rid> <version>");
    return 1;
}

var root = Path.GetFullPath(".");

var output = Path.Combine(root, "artifacts", rid);

var symbols = Path.Combine(root, "artifacts", "symbols", rid);

if (Directory.Exists(output))
{
    Directory.Delete(output, true);
}

BuildTools.Run("dotnet", "publish", Path.Combine(root, "src/PdfViewerLite.App/PdfViewerLite.App.csproj"), "-c", "Release", "-f", "net10.0", "-r", rid, "-o", output, $"-p:Version={version}");

_ = Directory.CreateDirectory(symbols);

foreach (var file in Directory.EnumerateFiles(output))
{
    if (Path.GetExtension(file) is ".dbg" or ".pdb")
    {
        File.Move(file, Path.Combine(symbols, Path.GetFileName(file)), true);
    }
}

foreach (var directory in Directory.EnumerateDirectories(output, "*.dSYM"))
{
    var target = Path.Combine(symbols, Path.GetFileName(directory));
    if (Directory.Exists(target))
    {
        Directory.Delete(target, true);
    }

    Directory.Move(directory, target);
}

Console.WriteLine($"Published {rid} to {output}");

return 0;
