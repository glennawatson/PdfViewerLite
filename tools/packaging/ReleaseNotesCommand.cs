// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Text;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Handles the release notes command.</summary>
internal static class ReleaseNotesCommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The command exit code.</returns>
    /// <exception cref="InvalidOperationException">A required setting or tool is unavailable.</exception>
    /// <exception cref="InvalidDataException">The input data is invalid.</exception>
    internal static int Run(string[] args)
    {
        if (args is not [var version, var sourceSha, var output])
        {
            Console.Error.WriteLine("Usage: PdfViewerLite.Tools release-notes <version> <source-sha> <output.md>");
            return 1;
        }

        var tags = Process.RunAndCaptureText("git", ["tag", "--merged", sourceSha, "--sort=-version:refname"]);
        if (tags.ExitStatus.ExitCode != 0)
        {
            throw new InvalidOperationException(tags.StandardError);
        }

        var baseRef = FindBaseRef(tags.StandardOutput, sourceSha);

        var commits = Process.RunAndCaptureText("git", ["log", "--no-merges", "--format=%s", $"{baseRef}..{sourceSha}"]);
        if (commits.ExitStatus.ExitCode != 0)
        {
            throw new InvalidOperationException(commits.StandardError);
        }

        var notes = new StringBuilder();
        _ = notes.Append("# ").AppendLine(version).AppendLine();
        foreach (var subject in commits.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            _ = notes.Append("- ").AppendLine(subject);
        }

        File.WriteAllText(output, notes.ToString());
        return 0;
    }

    /// <summary>Finds the preceding application tag or the root commit.</summary>
    /// <param name="tagList">The tagList.</param>
    /// <param name="sourceSha">The sourceSha.</param>
    /// <returns>The base reference.</returns>
    /// <exception cref="InvalidOperationException">The Git command fails.</exception>
    private static string FindBaseRef(string tagList, string sourceSha)
    {
        string? baseRef = null;
        foreach (var tag in tagList.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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

        return baseRef;
    }
}
