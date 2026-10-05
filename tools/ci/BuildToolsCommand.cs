// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using PdfViewerLite.Tools.Packaging;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Handles the build tools command.</summary>
internal static class BuildToolsCommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The command exit code.</returns>
    internal static int Run(string[] args)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(args.Length, 0);
        BuildTools.Run("dotnet", "build", "tools/PdfViewerLite.Tools/PdfViewerLite.Tools.csproj", "-c", "Release");
        BuildTools.Run("dotnet", "build", "tools/PdfViewerLite.AllocationAudit/PdfViewerLite.AllocationAudit.csproj", "-c", "Release");

        return 0;
    }
}
