// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;

namespace PdfViewerLite.Tools.Packaging;

/// <summary>Writes the Arch User Repository files for the binary package built from the release DEBs.</summary>
internal static class AurPackage
{
    /// <summary>The AUR package name.</summary>
    internal const string PackageName = "pdfviewerlite-bin";

    /// <summary>The Arch x86-64 architecture.</summary>
    internal const string X64 = "x86_64";

    /// <summary>The Arch ARM64 architecture.</summary>
    internal const string Arm64 = "aarch64";

    /// <summary>The package release number for a new upstream version.</summary>
    private const int Release = 1;

    /// <summary>The makepkg options field.</summary>
    private const string Options = "options";

    /// <summary>Keeps the Native AOT binary unstripped; release builds already move symbols out.</summary>
    private const string NoStrip = "!strip";

    /// <summary>Skips the debug package, which has no symbols to hold.</summary>
    private const string NoDebug = "!debug";

    /// <summary>Converts a semantic version to an Arch package version.</summary>
    /// <param name="version">The semantic version.</param>
    /// <returns>A version where a pre-release sorts before its release.</returns>
    internal static string ToArchVersion(string version)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        var withoutBuild = version.Split('+')[0];
        var dash = withoutBuild.IndexOf('-', StringComparison.Ordinal);
        if (dash < 0 || dash == withoutBuild.Length - 1)
        {
            return withoutBuild.TrimEnd('-');
        }

        // pacman's vercmp ranks a trailing letter segment below the bare version but a trailing separator above it,
        // so the pre-release joins the core version directly: 1.2.0-rc.1 becomes 1.2.0rc.1.
        var preRelease = Sanitize(withoutBuild[(dash + 1)..]);
        return char.IsAsciiLetter(preRelease[0])
            ? $"{withoutBuild[..dash]}{preRelease}"
            : $"{withoutBuild[..dash]}pre{preRelease}";
    }

    /// <summary>Gets the release download address of a DEB.</summary>
    /// <param name="tag">The release tag.</param>
    /// <param name="debFileName">The DEB file name.</param>
    /// <returns>The download address.</returns>
    internal static string GetDownloadUrl(string tag, string debFileName) => $"{LinuxPayload.Homepage}/releases/download/{tag}/{debFileName}";

    /// <summary>Creates the PKGBUILD.</summary>
    /// <param name="version">The semantic version, which is also the release tag.</param>
    /// <param name="x64Sha256">The SHA-256 of the amd64 DEB.</param>
    /// <param name="arm64Sha256">The SHA-256 of the arm64 DEB.</param>
    /// <returns>The PKGBUILD text.</returns>
    internal static string CreatePkgbuild(string version, string x64Sha256, string arm64Sha256)
    {
        var builder = new StringBuilder();
        _ = builder.Append("# Maintainer: ").Append(LinuxPayload.Vendor).Append('\n')
            .Append("# Edit tools/packaging/AurPackage.cs, not this file; each release rewrites it.\n")
            .Append("pkgname=").Append(PackageName).Append('\n')
            .Append("pkgver=").Append(ToArchVersion(version)).Append('\n')
            .Append("pkgrel=").Append(Release).Append('\n')
            .Append("pkgdesc=").Append(Quote(LinuxPayload.Summary)).Append('\n')
            .Append("arch=(").Append(Quote(X64)).Append(' ').Append(Quote(Arm64)).Append(")\n")
            .Append("url=").Append(Quote(LinuxPayload.Homepage)).Append('\n')
            .Append("license=(").Append(Quote(LinuxPayload.License)).Append(")\n");
        AppendArray(builder, "depends", LinuxPayload.ArchDepends);
        AppendArray(builder, "optdepends", LinuxPayload.ArchOptionalDepends);
        AppendArray(builder, "provides", [LinuxPayload.PackageName]);
        AppendArray(builder, "conflicts", [LinuxPayload.PackageName]);
        AppendArray(builder, Options, [NoStrip, NoDebug]);
        AppendArray(builder, $"source_{X64}", [GetSource(version, X64)]);
        AppendArray(builder, $"source_{Arm64}", [GetSource(version, Arm64)]);
        AppendArray(builder, $"sha256sums_{X64}", [x64Sha256]);
        AppendArray(builder, $"sha256sums_{Arm64}", [arm64Sha256]);
        _ = builder.Append('\n')
            .Append("package() {\n")
            .Append("  # makepkg unpacks the DEB; its data archive already uses the Arch /usr layout.\n")
            .Append("  bsdtar -xf data.tar.gz -C \"$pkgdir\"\n")
            .Append("}\n");
        return builder.ToString();
    }

    /// <summary>Creates the .SRCINFO that the AUR reads instead of running the PKGBUILD.</summary>
    /// <param name="version">The semantic version, which is also the release tag.</param>
    /// <param name="x64Sha256">The SHA-256 of the amd64 DEB.</param>
    /// <param name="arm64Sha256">The SHA-256 of the arm64 DEB.</param>
    /// <returns>The .SRCINFO text.</returns>
    internal static string CreateSrcinfo(string version, string x64Sha256, string arm64Sha256)
    {
        var builder = new StringBuilder();
        _ = builder.Append("pkgbase = ").Append(PackageName).Append('\n');
        AppendField(builder, "pkgdesc", LinuxPayload.Summary);
        AppendField(builder, "pkgver", ToArchVersion(version));
        AppendField(builder, "pkgrel", Release.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AppendField(builder, "url", LinuxPayload.Homepage);
        AppendField(builder, "arch", X64);
        AppendField(builder, "arch", Arm64);
        AppendField(builder, "license", LinuxPayload.License);
        AppendFields(builder, "depends", LinuxPayload.ArchDepends);
        AppendFields(builder, "optdepends", LinuxPayload.ArchOptionalDepends);
        AppendField(builder, "provides", LinuxPayload.PackageName);
        AppendField(builder, "conflicts", LinuxPayload.PackageName);
        AppendField(builder, Options, NoStrip);
        AppendField(builder, Options, NoDebug);
        AppendField(builder, $"source_{X64}", GetSource(version, X64));
        AppendField(builder, $"sha256sums_{X64}", x64Sha256);
        AppendField(builder, $"source_{Arm64}", GetSource(version, Arm64));
        AppendField(builder, $"sha256sums_{Arm64}", arm64Sha256);
        _ = builder.Append('\n').Append("pkgname = ").Append(PackageName).Append('\n');
        return builder.ToString();
    }

    /// <summary>Gets the DEB file name a release publishes for an Arch architecture.</summary>
    /// <param name="version">The semantic version.</param>
    /// <param name="architecture">The Arch architecture.</param>
    /// <returns>The DEB file name.</returns>
    internal static string GetDebFileName(string version, string architecture) =>
        $"{LinuxPayload.PackageName}_{DebBuilder.ToDebianVersion(version)}_{(architecture == X64 ? "amd64" : "arm64")}.deb";

    /// <summary>Gets a makepkg source entry that names the download after the Arch version and architecture.</summary>
    /// <param name="version">The semantic version.</param>
    /// <param name="architecture">The Arch architecture.</param>
    /// <returns>The source entry.</returns>
    private static string GetSource(string version, string architecture) =>
        $"{LinuxPayload.PackageName}-{ToArchVersion(version)}-{architecture}.deb::{GetDownloadUrl(version, GetDebFileName(version, architecture))}";

    /// <summary>Replaces characters pacman does not allow in a version with periods.</summary>
    /// <param name="value">The pre-release text.</param>
    /// <returns>The text with only letters, digits, periods and underscores.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Sanitize(string value) =>
        string.Create(value.Length, value, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = char.IsAsciiLetterOrDigit(source[i]) || source[i] is '.' or '_' ? source[i] : '.';
            }
        });

    /// <summary>Single-quotes a value for bash.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The quoted value.</returns>
    private static string Quote(string value) => $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'";

    /// <summary>Appends a bash array assignment.</summary>
    /// <param name="builder">The PKGBUILD text.</param>
    /// <param name="name">The array name.</param>
    /// <param name="values">The values.</param>
    private static void AppendArray(StringBuilder builder, string name, string[] values)
    {
        _ = builder.Append(name).Append("=(");
        for (var i = 0; i < values.Length; i++)
        {
            _ = builder.Append(i == 0 ? string.Empty : " ").Append(Quote(values[i]));
        }

        _ = builder.Append(")\n");
    }

    /// <summary>Appends one indented .SRCINFO field.</summary>
    /// <param name="builder">The .SRCINFO text.</param>
    /// <param name="name">The field name.</param>
    /// <param name="value">The value.</param>
    private static void AppendField(StringBuilder builder, string name, string value) =>
        _ = builder.Append('\t').Append(name).Append(" = ").Append(value).Append('\n');

    /// <summary>Appends one .SRCINFO field per value.</summary>
    /// <param name="builder">The .SRCINFO text.</param>
    /// <param name="name">The field name.</param>
    /// <param name="values">The values.</param>
    private static void AppendFields(StringBuilder builder, string name, string[] values)
    {
        foreach (var value in values)
        {
            AppendField(builder, name, value);
        }
    }
}
