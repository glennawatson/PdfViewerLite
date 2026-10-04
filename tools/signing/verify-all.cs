#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:include ../packaging/BuildTools.cs

using PdfViewerLite.Tools.Packaging;

if (args is not [var folder])
{
    Console.Error.WriteLine("Usage: dotnet run --file verify-all.cs -- <folder>");
    return 1;
}

var packages = Directory.GetFiles(folder, "*.msi*");

if (packages.Length == 0)
{
    throw new FileNotFoundException("No Windows installers were downloaded.");
}

foreach (var package in packages)
{
    var verifier = Path.GetExtension(package).ToLowerInvariant() switch
    {
        ".msix" => "tools/signing/verify-msix.cs",
        ".msi" => "tools/signing/verify-msi.cs",
        _ => throw new InvalidDataException($"Unsupported installer: {package}")
    };

    BuildTools.Run("dotnet", "run", "--file", verifier, "--", package);
}

return 0;
