// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using PdfViewerLite.Tools.Packaging;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Handles the icons command.</summary>
internal static class IconsCommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The command exit code.</returns>
    internal static int Run(string[] args)
    {
        if (args is not [])
        {
            Console.Error.WriteLine("Usage: PdfViewerLite.Tools icons  (run from the repository root)");
            return 1;
        }

        const string applicationId = "net.glennwatson.PdfViewerLite";
        const string sourcePng = "HyperPDFViewerIcon.png";
        const string sourceIco = "HyperPDFViewerIcon.ico";
        foreach (var path in IconBuilder.WriteLinuxIcons(sourcePng, "packaging/linux/icons/hicolor", applicationId))
        {
            Console.WriteLine(path);
        }

        IconBuilder.WriteIcns(sourcePng, "packaging/macos/PdfViewerLite.icns");
        Console.WriteLine("packaging/macos/PdfViewerLite.icns");
        File.Copy(sourceIco, "packaging/windows/PdfViewerLite.ico", true);
        Console.WriteLine("packaging/windows/PdfViewerLite.ico");
        return 0;
    }
}
