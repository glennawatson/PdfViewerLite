// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using PdfViewerLite.Tools.Packaging;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Installs and launches Linux and macOS packages on their native CI runners.</summary>
internal static class CheckInstalledPackagesCommand
{
    /// <summary>The required command argument count.</summary>
    private const int ArgumentCount = 2;

    /// <summary>The Apple disk image utility.</summary>
    private const string DiskImage = "hdiutil";

    /// <summary>The Linux package manager.</summary>
    private const string Apt = "apt-get";

    /// <summary>The install subcommand.</summary>
    private const string Install = "install";

    /// <summary>The container client.</summary>
    private const string Docker = "docker";

    /// <summary>The display used by Linux installation checks.</summary>
    private const string Display = ":99";

    /// <summary>The isolated Fedora container name.</summary>
    private const string Container = "pdfviewerlite-package-check";

    /// <summary>Checks packages produced by the current build.</summary>
    /// <param name="args">The runtime identifier and artifacts folder.</param>
    /// <returns>The command exit code.</returns>
    /// <exception cref="PlatformNotSupportedException">The runner is not Linux or macOS.</exception>
    internal static async Task<int> RunAsync(string[] args)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(args.Length, ArgumentCount);
        var packages = Path.GetFullPath(args[1]);
        var scratch = Path.Combine(Path.GetTempPath(), $"package-install-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(scratch);
        var pdf = Path.Combine(scratch, "installation-check.pdf");
        _ = await CreateCheckPdfCommand.RunAsync([pdf]).ConfigureAwait(false);
        try
        {
            if (OperatingSystem.IsMacOS())
            {
                await CheckMacAsync(packages, scratch, pdf).ConfigureAwait(false);
            }
            else if (OperatingSystem.IsLinux())
            {
                await CheckLinuxAsync(args[0], packages, scratch, pdf).ConfigureAwait(false);
            }
            else
            {
                throw new PlatformNotSupportedException("Use check-windows-packages on Windows.");
            }
        }
        finally
        {
            Directory.Delete(scratch, true);
        }

        return 0;
    }

    /// <summary>Installs both Mac distributions and checks their signed application bundles.</summary>
    /// <param name="packages">The package folder.</param>
    /// <param name="scratch">The temporary folder.</param>
    /// <param name="pdf">The check PDF.</param>
    /// <returns>A task.</returns>
    private static async Task CheckMacAsync(string packages, string scratch, string pdf)
    {
        var dmg = Find(packages, "*.dmg");
        var mount = Path.Combine(scratch, "mounted");
        _ = Directory.CreateDirectory(mount);
        BuildTools.Run(DiskImage, "verify", dmg);
        BuildTools.Run(DiskImage, "attach", "-nobrowse", "-readonly", "-mountpoint", mount, dmg);
        try
        {
            await InstallMacAsync(Single(Directory.GetDirectories(mount, "*.app")), scratch, pdf).ConfigureAwait(false);
        }
        finally
        {
            BuildTools.Run(DiskImage, "detach", mount);
        }

        var unpacked = Path.Combine(scratch, "zip");
        BuildTools.Run("ditto", "-x", "-k", Find(packages, "*.app.zip"), unpacked);
        await InstallMacAsync(Single(Directory.GetDirectories(unpacked, "*.app")), scratch, pdf).ConfigureAwait(false);
    }

    /// <summary>Copies a bundle into a temporary Applications folder and opens a PDF.</summary>
    /// <param name="app">The packaged bundle.</param>
    /// <param name="scratch">The temporary folder.</param>
    /// <param name="pdf">The check PDF.</param>
    /// <returns>A task.</returns>
    private static async Task InstallMacAsync(string app, string scratch, string pdf)
    {
        var installed = Path.Combine(scratch, "Applications", "Hyper PDF Viewer.app");
        if (Directory.Exists(installed))
        {
            Directory.Delete(installed, true);
        }

        BuildTools.Run("ditto", app, installed);
        BuildTools.Run("codesign", "--verify", "--deep", "--strict", installed);
        await PackageLaunch.CheckAsync(Path.Combine(installed, "Contents", "MacOS", LinuxPayload.PackageName), pdf).ConfigureAwait(false);
    }

    /// <summary>Installs DEB and RPM packages and launches the portable distributions.</summary>
    /// <param name="rid">The runtime identifier.</param>
    /// <param name="packages">The package folder.</param>
    /// <param name="scratch">The temporary folder.</param>
    /// <param name="pdf">The check PDF.</param>
    /// <returns>A task.</returns>
    /// <exception cref="PlatformNotSupportedException">The host is not Linux.</exception>
    /// <exception cref="InvalidOperationException">The test display cannot start.</exception>
    private static async Task CheckLinuxAsync(string rid, string packages, string scratch, string pdf)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException();
        }

        Environment.SetEnvironmentVariable("DISPLAY", Display);
        Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", null);
        Environment.SetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS", "unix:path=/dev/null");
        Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", scratch);
        File.SetUnixFileMode(scratch, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        BuildTools.Run("sudo", Apt, Install, "-y", "xvfb", "libfontconfig1", "libx11-6", "libice6", "libsm6", "libxrandr2", "libxi6", "libxcursor1", "libglib2.0-0t64", "libegl1", "libgl1");
        using var display = Process.Start(new ProcessStartInfo("Xvfb") { UseShellExecute = false, ArgumentList = { Display, "-screen", "0", "1280x800x24" } })
            ?? throw new InvalidOperationException("Could not start the X11 display.");
        try
        {
            BuildTools.Run("sudo", Apt, Install, "-y", Find(packages, "*.deb"));
            try
            {
                await PackageLaunch.CheckAsync("/usr/bin/pdfviewerlite", pdf).ConfigureAwait(false);
            }
            finally
            {
                BuildTools.Run("sudo", Apt, "remove", "-y", LinuxPayload.PackageName);
            }

            var unpacked = Path.Combine(scratch, "tar");
            _ = Directory.CreateDirectory(unpacked);
            await using (var source = File.OpenRead(Find(packages, "*.tar.gz")))
            await using (var gzip = new GZipStream(source, CompressionMode.Decompress))
            {
                await TarFile.ExtractToDirectoryAsync(gzip, unpacked, true).ConfigureAwait(false);
            }

            await PackageLaunch.CheckAsync(Single(Directory.GetFiles(unpacked, LinuxPayload.PackageName, SearchOption.AllDirectories)), pdf).ConfigureAwait(false);
            var image = Find(packages, "*.AppImage");
            File.SetUnixFileMode(image, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Environment.SetEnvironmentVariable("APPIMAGE_EXTRACT_AND_RUN", "1");
            await PackageLaunch.CheckAsync(image, pdf).ConfigureAwait(false);
            await CheckRpmAsync(rid, packages, scratch).ConfigureAwait(false);
        }
        finally
        {
            if (!display.HasExited)
            {
                display.Kill(entireProcessTree: true);
            }
        }
    }

    /// <summary>Installs and checks an RPM in a Fedora container of the runner's architecture.</summary>
    /// <param name="rid">The runtime identifier.</param>
    /// <param name="packages">The package folder.</param>
    /// <param name="scratch">The temporary folder containing the check PDF.</param>
    /// <returns>A task.</returns>
    private static async Task CheckRpmAsync(string rid, string packages, string scratch)
    {
        BuildTools.Run(
            Docker,
            "run",
            "-d",
            "--platform",
            rid == "linux-arm64" ? "linux/arm64" : "linux/amd64",
            "--name",
            Container,
            "--mount",
            $"type=bind,source={packages},target=/packages,readonly",
            "--mount",
            $"type=bind,source={scratch},target=/check,readonly",
            "fedora:45",
            "sleep",
            "infinity");
        try
        {
            BuildTools.Run(
                Docker,
                "exec",
                Container,
                "dnf",
                Install,
                "-y",
                $"/packages/{Path.GetFileName(Find(packages, "*.rpm"))}",
                "xorg-x11-server-Xvfb",
                "libicu",
                "libXrandr",
                "libXi",
                "libXcursor",
                "mesa-libEGL",
                "mesa-libGL",
                "glib2");
            BuildTools.Run(Docker, "exec", "-d", Container, "Xvfb", Display, "-screen", "0", "1280x800x24");
            await PackageLaunch.CheckAsync(Docker, "exec", "-e", $"DISPLAY={Display}", Container, "/usr/bin/pdfviewerlite", "/check/installation-check.pdf").ConfigureAwait(false);
            BuildTools.Run(Docker, "exec", Container, "rpm", "-V", LinuxPayload.PackageName);
            BuildTools.Run(Docker, "exec", Container, "dnf", "remove", "-y", LinuxPayload.PackageName);
        }
        finally
        {
            BuildTools.Run(Docker, "rm", "-f", Container);
        }
    }

    /// <summary>Finds exactly one package of the requested format.</summary>
    /// <param name="folder">The package folder.</param>
    /// <param name="pattern">The file pattern.</param>
    /// <returns>The absolute package path.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Find(string folder, string pattern) => Single(Directory.GetFiles(folder, pattern));

    /// <summary>Requires exactly one matching package or bundle.</summary>
    /// <param name="paths">The matching paths.</param>
    /// <returns>The single match.</returns>
    /// <exception cref="InvalidOperationException">The package is missing or ambiguous.</exception>
    private static string Single(string[] paths)
    {
        if (paths.Length != 1)
        {
            throw new InvalidOperationException($"Expected one packaged asset; found {paths.Length}.");
        }

        return paths[0];
    }
}
