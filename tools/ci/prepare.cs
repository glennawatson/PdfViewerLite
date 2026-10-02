#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:include ../packaging/BuildTools.cs

using PdfViewerLite.Tools.Packaging;

if (args is not [var mode] || mode is not ("test" or "package"))
{
    Console.Error.WriteLine("Usage: dotnet run --file prepare.cs -- <test|package>");
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
    BuildTools.Run("sudo", packageManager, "install", "-y", "libtesseract5", "tesseract-ocr-eng", "pulseaudio");
    BuildTools.Run("pulseaudio", "--daemonize=true", "--exit-idle-time=-1", "-n", "--load=module-null-sink sink_name=silent", "--load=module-native-protocol-unix");
}
else
{
    BuildTools.Run("sudo", packageManager, "install", "-y", "clang", "zlib1g-dev", "desktop-file-utils", "xvfb", "dbus", "at-spi2-core");
}

return 0;
