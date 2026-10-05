// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using PdfViewerLite.Tools.Packaging;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Handles the prepare command.</summary>
internal static class PrepareCommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The command exit code.</returns>
    internal static int Run(string[] args)
    {
        if (args is not [var mode] || mode is not ("test" or "package"))
        {
            Console.Error.WriteLine("Usage: PdfViewerLite.Tools ci prepare <test|package>");
            return 1;
        }

        if (!OperatingSystem.IsLinux())
        {
            return 0;
        }

        const string packageManager = "apt-get";
        BuildTools.Run("sudo", packageManager, "update");
        if (mode == "test")
        {
            // No system Tesseract: the text recognition tests must pass with the copy and English data the app ships.
            BuildTools.Run("sudo", packageManager, "install", "-y", "pulseaudio");
            BuildTools.Run("pulseaudio", "--daemonize=true", "--exit-idle-time=-1", "-n", "--load=module-null-sink sink_name=silent", "--load=module-native-protocol-unix");
        }
        else
        {
            BuildTools.Run("sudo", packageManager, "install", "-y", "clang", "zlib1g-dev", "desktop-file-utils", "xvfb", "dbus", "at-spi2-core");
        }

        return 0;
    }
}
