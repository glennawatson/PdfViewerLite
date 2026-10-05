// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using PdfViewerLite.Tools.Packaging;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Handles the publish command.</summary>
internal static class PublishCommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The command exit code.</returns>
    internal static int Run(string[] args)
    {
        if (args is not [var rid, var version] || !IsSupportedRid(rid))
        {
            Console.Error.WriteLine("Usage: PdfViewerLite.Tools publish <rid> <version>");
            return 1;
        }

        var root = Path.GetFullPath(".");
        var output = Path.Combine(root, "artifacts", rid);
        var symbols = Path.Combine(root, "artifacts", "symbols", rid);
        if (Directory.Exists(output))
        {
            Directory.Delete(output, true);
        }

        BuildTools.Run(
            "dotnet",
            "publish",
            Path.Combine(root, "src/PdfViewerLite.App/PdfViewerLite.App.csproj"),
            "-c",
            "Release",
            "-f",
            "net10.0",
            "-r",
            rid,
            "-o",
            output,
            $"-p:Version={version}",
            $"-p:MinVerVersionOverride={version}");
        _ = Directory.CreateDirectory(symbols);
        MoveSymbols(output, symbols);

        Console.WriteLine($"Published {rid} to {output}");
        return 0;
    }

    /// <summary>Moves debug symbols out of the published application.</summary>
    /// <param name="output">The output.</param>
    /// <param name="symbols">The symbols.</param>
    private static void MoveSymbols(string output, string symbols)
    {
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
    }

    /// <summary>Checks the supported publish runtime identifiers.</summary>
    /// <param name="rid">The runtime identifier.</param>
    /// <returns>Whether the runtime is supported.</returns>
    private static bool IsSupportedRid(string rid) => rid is "linux-x64" or "linux-arm64" or "win-x64" or "win-arm64" or "osx-x64" or "osx-arm64";
}
