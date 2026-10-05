// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Tools.Packaging;

/// <summary>Describes what the Linux packages install and the package metadata.</summary>
internal static class LinuxPayload
{
    /// <summary>The reverse DNS application identifier.</summary>
    internal const string ApplicationId = "net.glennwatson.PdfViewerLite";

    /// <summary>The package and executable name.</summary>
    internal const string PackageName = "pdfviewerlite";

    /// <summary>The one line package summary.</summary>
    internal const string Summary = "Fast, tabbed PDF viewer";

    /// <summary>The package description paragraph.</summary>
    internal const string Description =
        "Renders with PDFium, follows the KDE colour scheme and opens documents launched\nfrom the file manager as tabs in the running window.";

    /// <summary>The project home page.</summary>
    internal const string Homepage = "https://github.com/glennawatson/PdfViewerLite";

    /// <summary>The SPDX licence identifier.</summary>
    internal const string License = "MIT";

    /// <summary>The package author and publisher.</summary>
    internal const string Vendor = "Glenn Watson";

    /// <summary>The Debian runtime dependencies.</summary>
    internal static readonly string[] DebianDepends =
    [
        "libfontconfig1 | fontconfig",
        "libx11-6 | libX11",
        "libice6 | libICE",
        "libsm6 | libSM",
    ];

    /// <summary>The Debian optional dependencies.</summary>
    internal static readonly string[] DebianRecommends =
    [
        "libwayland-client0",
        "libxkbcommon0",
        "libegl1",
        "libcups2",
        "libpulse0",
    ];

    /// <summary>The RPM runtime dependencies.</summary>
    internal static readonly string[] RpmDepends = ["fontconfig", "libX11", "libICE", "libSM"];

    /// <summary>The RPM optional dependencies.</summary>
    internal static readonly string[] RpmRecommends =
    [
        "libwayland-client",
        "libxkbcommon",
        "mesa-libEGL",
        "cups-libs",
        "pulseaudio-libs",
    ];

    /// <summary>The Arch Linux runtime dependencies.</summary>
    internal static readonly string[] ArchDepends = ["fontconfig", "libx11", "libice", "libsm", "gcc-libs", "glibc"];

    /// <summary>The Arch Linux optional dependencies, each with the reason it helps.</summary>
    internal static readonly string[] ArchOptionalDepends =
    [
        "wayland: native Wayland support",
        "libxkbcommon: Wayland keyboard input",
        "libegl: native Wayland rendering",
        "libcups: print straight to a printer",
        "libpulse: Read Aloud sound through PulseAudio or PipeWire",
    ];

    /// <summary>The time stamped on every packaged entry.</summary>
    internal static readonly DateTimeOffset BuildTime = DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds());

    /// <summary>Permission bits for an executable file (rwxr-xr-x).</summary>
    private const int ExecutableMode = 0b1_1110_1101;

    /// <summary>Permission bits for a regular file (rw-r--r--).</summary>
    private const int RegularMode = 0b1_1010_0100;

    /// <summary>Permission bits for a symbolic link (rwxrwxrwx).</summary>
    private const int LinkMode = 0b1_1111_1111;

    /// <summary>The installed library directory.</summary>
    private const string LibraryDirectory = "/usr/lib/pdfviewerlite";

    /// <summary>The installed licence directory.</summary>
    private const string LicenseDirectory = "/usr/share/licenses/pdfviewerlite";

    /// <summary>The repository folder holding the hicolor PNG icons made by icons.cs.</summary>
    private const string IconRoot = "packaging/linux/icons/hicolor";

    /// <summary>The size folder of the icon placed at the root of an AppImage.</summary>
    private const string AppImageIconSize = "256x256";

    /// <summary>Creates the entries for the deb and rpm packages.</summary>
    /// <param name="publishDirectory">The published application folder.</param>
    /// <returns>The entries sorted by path.</returns>
    internal static List<PayloadEntry> CreateInstalled(string publishDirectory)
    {
        List<PayloadEntry> entries =
        [
            new(LibraryDirectory, PayloadKind.Directory, ExecutableMode, null, null),
            new(LicenseDirectory, PayloadKind.Directory, ExecutableMode, null, null),
            new($"/usr/bin/{PackageName}", PayloadKind.Symlink, LinkMode, null, $"{LibraryDirectory}/{PackageName}"),
            new($"/usr/share/applications/{ApplicationId}.desktop", PayloadKind.File, RegularMode, $"packaging/linux/{ApplicationId}.desktop", null),
            new($"/usr/share/metainfo/{ApplicationId}.metainfo.xml", PayloadKind.File, RegularMode, $"packaging/linux/{ApplicationId}.metainfo.xml", null),
            new($"{LicenseDirectory}/LICENSE", PayloadKind.File, RegularMode, "LICENSE", null),
        ];

        AddPublished(entries, publishDirectory, LibraryDirectory);
        AddIcons(entries);
        entries.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));
        return entries;
    }

    /// <summary>Creates the entries of an AppImage application directory.</summary>
    /// <param name="publishDirectory">The published application folder.</param>
    /// <returns>The entries sorted by path.</returns>
    internal static List<PayloadEntry> CreateAppDirectory(string publishDirectory)
    {
        List<PayloadEntry> entries =
        [
            new("/AppRun", PayloadKind.Symlink, LinkMode, null, $"usr/bin/{PackageName}"),
            new("/.DirIcon", PayloadKind.Symlink, LinkMode, null, $"{ApplicationId}.png"),
            new($"/{ApplicationId}.desktop", PayloadKind.File, RegularMode, $"packaging/linux/{ApplicationId}.desktop", null),
            new($"/{ApplicationId}.png", PayloadKind.File, RegularMode, $"{IconRoot}/{AppImageIconSize}/apps/{ApplicationId}.png", null),
            new($"/usr/share/applications/{ApplicationId}.desktop", PayloadKind.File, RegularMode, $"packaging/linux/{ApplicationId}.desktop", null),
            new($"/usr/share/metainfo/{ApplicationId}.appdata.xml", PayloadKind.File, RegularMode, $"packaging/linux/{ApplicationId}.metainfo.xml", null),
        ];

        AddPublished(entries, publishDirectory, "/usr/bin");
        AddIcons(entries);
        entries.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));
        return entries;
    }

    /// <summary>Adds the hicolor PNG icons at every size below the system icon theme.</summary>
    /// <param name="entries">The list to extend.</param>
    private static void AddIcons(List<PayloadEntry> entries)
    {
        foreach (var file in Directory.EnumerateFiles(IconRoot, "*.png", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(IconRoot, file).Replace('\\', '/');
            entries.Add(new($"/usr/share/icons/hicolor/{relative}", PayloadKind.File, RegularMode, file, null));
        }
    }

    /// <summary>Adds every published file and folder below an install folder.</summary>
    /// <param name="entries">The list to extend.</param>
    /// <param name="publishDirectory">The published application folder.</param>
    /// <param name="installDirectory">The absolute folder to install into.</param>
    private static void AddPublished(List<PayloadEntry> entries, string publishDirectory, string installDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(publishDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(publishDirectory, directory).Replace('\\', '/');
            entries.Add(new($"{installDirectory}/{relative}", PayloadKind.Directory, ExecutableMode, null, null));
        }

        foreach (var file in Directory.EnumerateFiles(publishDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(publishDirectory, file).Replace('\\', '/');
            entries.Add(new($"{installDirectory}/{relative}", PayloadKind.File, GetFileMode(file, relative), file, null));
        }
    }

    /// <summary>Gets the permission bits for a published file.</summary>
    /// <param name="file">The file on disk.</param>
    /// <param name="relative">The path relative to the published folder.</param>
    /// <returns>The permission bits.</returns>
    private static int GetFileMode(string file, string relative)
    {
        if (OperatingSystem.IsWindows())
        {
            return relative == PackageName ? ExecutableMode : RegularMode;
        }

        return (File.GetUnixFileMode(file) & UnixFileMode.UserExecute) == 0 ? RegularMode : ExecutableMode;
    }
}
