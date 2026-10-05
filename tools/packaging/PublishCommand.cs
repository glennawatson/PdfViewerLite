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
        if (rid.StartsWith("win-", StringComparison.Ordinal) && !CopyVisualCppRuntime(output, rid["win-".Length..]))
        {
            Console.Error.WriteLine($"The Visual C++ runtime for {rid} was not found under Visual Studio's VC\\Redist folder; the shipped Tesseract needs it.");
            return 1;
        }

        Console.WriteLine($"Published {rid} to {output}");
        return 0;
    }

    /// <summary>
    /// Copies the Visual C++ runtime beside the app. The shipped Tesseract needs it, and Windows does not always have it,
    /// so it is deployed app-locally from the Visual Studio redistributable folder that Native AOT publishing already needs.
    /// </summary>
    /// <param name="output">The published application folder.</param>
    /// <param name="architecture">The processor architecture, for example <c>x64</c>.</param>
    /// <returns><see langword="true"/> when the runtime was copied.</returns>
    private static bool CopyVisualCppRuntime(string output, string architecture)
    {
        string[] runtimeFiles = ["msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll"];
        if (FindVisualCppRuntime(architecture) is not { } folder)
        {
            return false;
        }

        foreach (var file in runtimeFiles)
        {
            var source = Path.Combine(folder, file);
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(output, file), true);
            }
        }

        return File.Exists(Path.Combine(output, runtimeFiles[0]));
    }

    /// <summary>Finds the newest Visual C++ runtime redistributable folder for an architecture.</summary>
    /// <param name="architecture">The processor architecture, for example <c>x64</c>.</param>
    /// <returns>The folder, or <see langword="null"/> when Visual Studio has none.</returns>
    private static string? FindVisualCppRuntime(string architecture)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio");
        if (!Directory.Exists(root))
        {
            return null;
        }

        string? newest = null;
        foreach (var version in EnumerateRedistVersions(root))
        {
            var target = Path.Combine(version, architecture);
            if (!Directory.Exists(target))
            {
                continue;
            }

            foreach (var crt in Directory.EnumerateDirectories(target, "Microsoft.VC14*.CRT"))
            {
                if (newest is null || string.CompareOrdinal(crt, newest) > 0)
                {
                    newest = crt;
                }
            }
        }

        return newest;
    }

    /// <summary>Lists the Visual C++ redistributable version folders, laid out as <c>&lt;year&gt;\&lt;edition&gt;\VC\Redist\MSVC\&lt;version&gt;</c>.</summary>
    /// <param name="root">The Visual Studio folder.</param>
    /// <returns>The version folders.</returns>
    private static List<string> EnumerateRedistVersions(string root)
    {
        var versions = new List<string>();
        foreach (var year in Directory.EnumerateDirectories(root))
        {
            foreach (var edition in Directory.EnumerateDirectories(year))
            {
                var redist = Path.Combine(edition, "VC", "Redist", "MSVC");
                if (Directory.Exists(redist))
                {
                    versions.AddRange(Directory.EnumerateDirectories(redist));
                }
            }
        }

        return versions;
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
