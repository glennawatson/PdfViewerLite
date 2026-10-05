// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using PdfViewerLite.Tools.Packaging;

namespace PdfViewerLite.Tools.Tests;

/// <summary>Tests for <see cref="AurPackage"/>.</summary>
public sealed class AurPackageTests
{
    /// <summary>The release version used by the tests.</summary>
    private const string Version = "1.0.1";

    /// <summary>A stand-in amd64 DEB hash.</summary>
    private const string X64Hash = "1111111111111111111111111111111111111111111111111111111111111111";

    /// <summary>A stand-in arm64 DEB hash.</summary>
    private const string Arm64Hash = "2222222222222222222222222222222222222222222222222222222222222222";

    /// <summary>Semantic versions become pacman versions that sort pre-releases before the release.</summary>
    /// <param name="version">The semantic version.</param>
    /// <param name="expected">The Arch package version.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("1.0.1", "1.0.1")]
    [Arguments("1.2.0-rc.1", "1.2.0rc.1")]
    [Arguments("1.2.0-beta.2+abc123", "1.2.0beta.2")]
    [Arguments("1.0.0-0.3", "1.0.0pre0.3")]
    [Arguments("1.0.0-x-y", "1.0.0x.y")]
    public async Task ConvertsSemanticVersions(string version, string expected) =>
        await Assert.That(AurPackage.ToArchVersion(version)).IsEqualTo(expected);

    /// <summary>The PKGBUILD downloads both release DEBs from the release tag and pins their hashes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PkgbuildDownloadsReleaseDebs()
    {
        var pkgbuild = AurPackage.CreatePkgbuild(Version, X64Hash, Arm64Hash);

        await Assert.That(pkgbuild).Contains("pkgname=pdfviewerlite-bin\n");
        await Assert.That(pkgbuild).Contains("pkgver=1.0.1\n");
        await Assert.That(pkgbuild).Contains(
            "source_x86_64=('pdfviewerlite-1.0.1-x86_64.deb::https://github.com/glennawatson/PdfViewerLite/releases/download/1.0.1/pdfviewerlite_1.0.1_amd64.deb')");
        await Assert.That(pkgbuild).Contains(
            "source_aarch64=('pdfviewerlite-1.0.1-aarch64.deb::https://github.com/glennawatson/PdfViewerLite/releases/download/1.0.1/pdfviewerlite_1.0.1_arm64.deb')");
        await Assert.That(pkgbuild).Contains($"sha256sums_x86_64=('{X64Hash}')");
        await Assert.That(pkgbuild).Contains($"sha256sums_aarch64=('{Arm64Hash}')");
        await Assert.That(pkgbuild).Contains("provides=('pdfviewerlite')");
        await Assert.That(pkgbuild).Contains("conflicts=('pdfviewerlite')");
        await Assert.That(pkgbuild).Contains("bsdtar -xf data.tar.gz -C \"$pkgdir\"");
    }

    /// <summary>Optional dependencies keep their reasons as single bash words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PkgbuildQuotesOptionalDependencies()
    {
        var pkgbuild = AurPackage.CreatePkgbuild(Version, X64Hash, Arm64Hash);

        await Assert.That(pkgbuild).Contains("'libcups: print straight to a printer'");
    }

    /// <summary>The .SRCINFO matches the PKGBUILD fields the AUR shows.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SrcinfoListsPackageFields()
    {
        var srcinfo = AurPackage.CreateSrcinfo("2.0.0-rc.1", X64Hash, Arm64Hash);

        await Assert.That(srcinfo).StartsWith("pkgbase = pdfviewerlite-bin\n");
        await Assert.That(srcinfo).Contains("\tpkgver = 2.0.0rc.1\n");
        await Assert.That(srcinfo).Contains("\tarch = x86_64\n\tarch = aarch64\n");
        await Assert.That(srcinfo).Contains("\tdepends = fontconfig\n");
        await Assert.That(srcinfo).Contains(
            "\tsource_x86_64 = pdfviewerlite-2.0.0rc.1-x86_64.deb::https://github.com/glennawatson/PdfViewerLite/releases/download/2.0.0-rc.1/pdfviewerlite_2.0.0~rc.1_amd64.deb\n");
        await Assert.That(srcinfo).Contains($"\tsha256sums_aarch64 = {Arm64Hash}\n");
        await Assert.That(srcinfo).EndsWith("\npkgname = pdfviewerlite-bin\n");
    }

    /// <summary>The DEB holds the data archive name that the PKGBUILD unpacks.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DebHoldsTheDataArchiveThePkgbuildUnpacks()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-aur-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        try
        {
            var executable = Path.Combine(folder, LinuxPayload.PackageName);
            await File.WriteAllTextAsync(executable, "stand-in");
            var deb = Path.Combine(folder, AurPackage.GetDebFileName(Version, AurPackage.X64));
            DebBuilder.Build([new("/usr/lib/pdfviewerlite/pdfviewerlite", PayloadKind.File, 0b1_1110_1101, executable, null)], Version, "amd64", deb);

            var bytes = await File.ReadAllBytesAsync(deb);
            await Assert.That(Path.GetFileName(deb)).IsEqualTo("pdfviewerlite_1.0.1_amd64.deb");
            await Assert.That(bytes.AsSpan().IndexOf("data.tar.gz"u8)).IsGreaterThan(0);
            await Assert.That(Encoding.ASCII.GetString(bytes, 0, "!<arch>\n".Length)).IsEqualTo("!<arch>\n");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
