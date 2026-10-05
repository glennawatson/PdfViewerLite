#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:include BuildTools.cs

using System.Diagnostics;

using PdfViewerLite.Tools.Packaging;

if (args is not [var version, var sourceSha, var output])
{
    Console.Error.WriteLine("Usage: dotnet run --file release-notes.cs -- <version> <source-sha> <output.md>");
    return 1;
}

var tags = Process.RunAndCaptureText("git", ["tag", "--merged", sourceSha, "--sort=-version:refname"]);

if (tags.ExitStatus.ExitCode != 0)
{
    throw new InvalidOperationException(tags.StandardError);
}

string? baseRef = null;

foreach (var tag in tags.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
{
    var core = tag.Split('-', '+')[0];
    if (!Version.TryParse(core, out var parsed) || parsed.Build < 0 || parsed.Revision >= 0)
    {
        continue;
    }

    baseRef = tag;
    break;
}

// Voice assets have orphan release tags. The first application release starts at its own root commit.
if (baseRef is null)
{
    var roots = Process.RunAndCaptureText("git", ["rev-list", "--max-parents=0", sourceSha]);
    if (roots.ExitStatus.ExitCode != 0)
    {
        throw new InvalidOperationException(roots.StandardError);
    }

    baseRef = roots.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0];
}

File.Delete(output);

BuildTools.Run("git-release-notes", "--release-version", version, "--base-ref", baseRef, "--head-ref", sourceSha, "--output-file", output);

if (!File.Exists(output) || new FileInfo(output).Length == 0)
{
    throw new InvalidDataException("Release note generation did not produce a non-empty notes file.");
}

return 0;
