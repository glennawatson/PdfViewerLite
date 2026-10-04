// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Tools.Packaging;

/// <summary>Builds AppImage packages with .NET only.</summary>
internal static class AppImageBuilder
{
    /// <summary>Permission bits for the AppImage file (rwxr-xr-x).</summary>
    private const UnixFileMode ExecutableMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    /// <summary>Builds an AppImage from the type 2 runtime and an application directory.</summary>
    /// <param name="runtime">The AppImage type 2 runtime executable.</param>
    /// <param name="appDirectory">The application directory entries.</param>
    /// <param name="output">The AppImage to create.</param>
    internal static void Build(string runtime, IReadOnlyList<PayloadEntry> appDirectory, string output)
    {
        using (var image = File.Create(output))
        {
            using (var runtimeStream = File.OpenRead(runtime))
            {
                runtimeStream.CopyTo(image);
            }

            SquashfsWriter.Write(image, appDirectory, LinuxPayload.BuildTime);
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(output, ExecutableMode);
        }
    }
}
