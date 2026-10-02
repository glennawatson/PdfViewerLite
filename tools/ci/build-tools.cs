#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:include ../packaging/BuildTools.cs

using PdfViewerLite.Tools.Packaging;

var files = Directory.GetFiles("tools", "*.cs", SearchOption.AllDirectories);

Array.Sort(files, StringComparer.Ordinal);

foreach (var file in files)
{
    using var reader = File.OpenText(file);
    if (reader.ReadLine() == "#!/usr/bin/env dotnet" && Path.GetFullPath(file) != Path.GetFullPath("tools/ci/build-tools.cs"))
    {
        BuildTools.Run("dotnet", "build", file, "-c", "Release");
    }
}

return 0;
