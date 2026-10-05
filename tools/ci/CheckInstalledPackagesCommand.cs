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

    /// <summary>The installed Mac application bundle name.</summary>
    private const string MacBundle = "Hyper PDF Viewer.app";

    /// <summary>The version of the older package built to check upgrades.</summary>
    private const string PreviousVersion = "0.0.1";

    /// <summary>The installed Linux executable.</summary>
    private const string LinuxExecutable = "/usr/bin/pdfviewerlite";

    /// <summary>The seeded user data folder inside the Fedora container.</summary>
    private const string ContainerUserData = "/root/package-check";

    /// <summary>The Linux runtime identifier for ARM64.</summary>
    private const string LinuxArm64 = "linux-arm64";

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
        var data = SeedUserData(scratch, pdf);
        BuildTools.Run(DiskImage, "attach", "-nobrowse", "-readonly", "-mountpoint", mount, dmg);
        try
        {
            await InstallMacAsync(Single(Directory.GetDirectories(mount, "*.app")), scratch, pdf).ConfigureAwait(false);
        }
        finally
        {
            BuildTools.Run(DiskImage, "detach", mount);
        }

        data.Verify("installing the DMG bundle");

        // Copying the ZIP bundle over the DMG bundle is how a person upgrades a Mac app.
        var unpacked = Path.Combine(scratch, "zip");
        BuildTools.Run("ditto", "-x", "-k", Find(packages, "*.app.zip"), unpacked);
        await InstallMacAsync(Single(Directory.GetDirectories(unpacked, "*.app")), scratch, pdf).ConfigureAwait(false);
        data.Verify("replacing the bundle from the app ZIP");
        Directory.Delete(Path.Combine(scratch, "Applications", MacBundle), true);
        data.Verify("removing the bundle");
    }

    /// <summary>Seeds preferences and a document, and points launched viewers at the seeded preferences.</summary>
    /// <param name="scratch">The temporary folder.</param>
    /// <param name="pdf">The check PDF to keep as the document.</param>
    /// <returns>The check for the seeded data.</returns>
    private static UserDataCheck SeedUserData(string scratch, string pdf)
    {
        var data = UserDataCheck.Seed(Path.Combine(scratch, "user"), pdf);
        Environment.SetEnvironmentVariable(UserDataCheck.ConfigHomeVariable, data.ConfigHome);
        return data;
    }

    /// <summary>Copies a bundle into a temporary Applications folder and opens a PDF.</summary>
    /// <param name="app">The packaged bundle.</param>
    /// <param name="scratch">The temporary folder.</param>
    /// <param name="pdf">The check PDF.</param>
    /// <returns>A task.</returns>
    private static async Task InstallMacAsync(string app, string scratch, string pdf)
    {
        var installed = Path.Combine(scratch, "Applications", MacBundle);
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
            await CheckDebAsync(rid, packages, scratch, pdf).ConfigureAwait(false);

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

    /// <summary>Installs an older DEB, upgrades it to the packaged DEB and removes it, keeping the person's data.</summary>
    /// <param name="rid">The runtime identifier.</param>
    /// <param name="packages">The package folder.</param>
    /// <param name="scratch">The temporary folder.</param>
    /// <param name="pdf">The check PDF.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The upgrade left the older version installed.</exception>
    private static async Task CheckDebAsync(string rid, string packages, string scratch, string pdf)
    {
        var previous = Path.Combine(scratch, $"{LinuxPayload.PackageName}-previous.deb");
        DebBuilder.Build(LinuxPayload.CreateInstalled(Path.Combine(packages, rid)), PreviousVersion, rid == LinuxArm64 ? "arm64" : "amd64", previous);
        var data = SeedUserData(scratch, pdf);
        BuildTools.Run("sudo", Apt, Install, "-y", previous);
        try
        {
            await PackageLaunch.CheckAsync(LinuxExecutable, pdf).ConfigureAwait(false);
            data.Verify("installing the older DEB");
            BuildTools.Run("sudo", Apt, Install, "-y", Find(packages, "*.deb"));
            var installed = await BuildTools.CaptureAsync("dpkg-query", "-W", "-f=${Version}", LinuxPayload.PackageName).ConfigureAwait(false);
            if (installed == DebBuilder.ToDebianVersion(PreviousVersion))
            {
                throw new InvalidOperationException("The DEB upgrade left the older version installed.");
            }

            await PackageLaunch.CheckAsync(LinuxExecutable, pdf).ConfigureAwait(false);
            data.Verify("upgrading the DEB");
        }
        finally
        {
            BuildTools.Run("sudo", Apt, "remove", "-y", LinuxPayload.PackageName);
        }

        data.Verify("removing the DEB");
    }

    /// <summary>Upgrades an older RPM to the packaged RPM and removes it in a Fedora container, keeping the person's data.</summary>
    /// <param name="rid">The runtime identifier.</param>
    /// <param name="packages">The package folder.</param>
    /// <param name="scratch">The temporary folder containing the check PDF.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The upgrade left the older version installed.</exception>
    private static async Task CheckRpmAsync(string rid, string packages, string scratch)
    {
        var isArm64 = rid == LinuxArm64;
        var previous = Path.Combine(scratch, $"{LinuxPayload.PackageName}-previous.rpm");
        RpmBuilder.Build(LinuxPayload.CreateInstalled(Path.Combine(packages, rid)), PreviousVersion, isArm64 ? "aarch64" : "x86_64", previous);
        var data = UserDataCheck.Seed(Path.Combine(scratch, "rpm-user"), Path.Combine(scratch, "installation-check.pdf"));
        BuildTools.Run(
            Docker,
            "run",
            "-d",
            "--platform",
            isArm64 ? "linux/arm64" : "linux/amd64",
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
                $"/check/{Path.GetFileName(previous)}",
                "xorg-x11-server-Xvfb",
                "libicu",
                "libXrandr",
                "libXi",
                "libXcursor",
                "mesa-libEGL",
                "mesa-libGL",
                "glib2");
            BuildTools.Run(Docker, "cp", data.Root, $"{Container}:{ContainerUserData}");
            BuildTools.Run(Docker, "exec", "-d", Container, "Xvfb", Display, "-screen", "0", "1280x800x24");
            await LaunchInContainerAsync().ConfigureAwait(false);
            BuildTools.Run(Docker, "exec", Container, "dnf", "upgrade", "-y", $"/packages/{Path.GetFileName(Find(packages, "*.rpm"))}");
            var installed = await BuildTools.CaptureAsync(Docker, "exec", Container, "rpm", "-q", "--qf", "%{VERSION}", LinuxPayload.PackageName).ConfigureAwait(false);
            if (installed == RpmBuilder.ToRpmVersion(PreviousVersion))
            {
                throw new InvalidOperationException("The RPM upgrade left the older version installed.");
            }

            await LaunchInContainerAsync().ConfigureAwait(false);
            BuildTools.Run(Docker, "exec", Container, "rpm", "-V", LinuxPayload.PackageName);
            BuildTools.Run(Docker, "exec", Container, "dnf", "remove", "-y", LinuxPayload.PackageName);
            var copied = Path.Combine(scratch, "rpm-user-after");
            BuildTools.Run(Docker, "cp", $"{Container}:{ContainerUserData}", copied);
            data.VerifyCopy(copied, "upgrading and removing the RPM");
        }
        finally
        {
            BuildTools.Run(Docker, "rm", "-f", Container);
        }
    }

    /// <summary>Opens the check PDF with the RPM-installed viewer, using the seeded preferences.</summary>
    /// <returns>A task.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task LaunchInContainerAsync() =>
        PackageLaunch.CheckAsync(
            Docker,
            "exec",
            "-e",
            $"DISPLAY={Display}",
            "-e",
            $"{UserDataCheck.ConfigHomeVariable}={ContainerUserData}/{UserDataCheck.ConfigFolder}",
            Container,
            LinuxExecutable,
            "/check/installation-check.pdf");

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
