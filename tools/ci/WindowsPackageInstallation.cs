// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using PdfViewerLite.Tools.Packaging;
using PdfViewerLite.Tools.Signing;
#if WINDOWS
using Windows.Management.Deployment;
#endif

namespace PdfViewerLite.Tools.Commands;

/// <summary>Signs CI fixtures with an ephemeral certificate and checks native Windows installation.</summary>
internal static class WindowsPackageInstallation
{
    /// <summary>The isolated MSIX fixture identity.</summary>
    private const string PackageIdentity = "PdfViewerLite.PackageCheck";

    /// <summary>The ephemeral certificate and fixture publisher.</summary>
    private const string TestPublisher = "CN=PdfViewerLite Package Check";

    /// <summary>The test RSA key size.</summary>
    private const int KeySize = 2048;

    /// <summary>The certificate clock skew allowance in minutes.</summary>
    private const int ClockSkew = -5;

#if WINDOWS
    /// <summary>The silent installer UI level.</summary>
    private const uint SilentUi = 2;

    /// <summary>The Windows Installer state that removes a product.</summary>
    private const int MsiStateAbsent = 2;

    /// <summary>The installed viewer executable.</summary>
    private const string Executable = "pdfviewerlite.exe";
#endif

    /// <summary>The disposable test keystore password.</summary>
    private const string Password = "package-check";

    /// <summary>The private test keystore file name.</summary>
    private const string KeyStore = "test-signing.pfx";

    /// <summary>The test public certificate file name.</summary>
    private const string PublicCertificate = "test-signing.cer";

    /// <summary>Creates a fresh test certificate.</summary>
    /// <param name="scratch">The temporary directory.</param>
    /// <returns>A task.</returns>
    internal static async Task PrepareAsync(string scratch)
    {
        using var key = RSA.Create(KeySize);
        var request = new CertificateRequest(TestPublisher, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new("1.3.6.1.5.5.7.3.3")], true));
        var now = TimeProvider.System.GetUtcNow();
        using var certificate = request.CreateSelfSigned(now.AddMinutes(ClockSkew), now.AddDays(1));
        await File.WriteAllBytesAsync(Path.Combine(scratch, KeyStore), certificate.Export(X509ContentType.Pfx, Password)).ConfigureAwait(false);
        await File.WriteAllBytesAsync(Path.Combine(scratch, PublicCertificate), certificate.Export(X509ContentType.Cert)).ConfigureAwait(false);
    }

    /// <summary>Signs a batch of payloads or installers with the temporary test key.</summary>
    /// <param name="scratch">The test keystore directory.</param>
    /// <param name="files">The files to sign.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Sign(string scratch, IEnumerable<string> files) =>
        BuildTools.Run(WindowsSignatureVerifier.FindSignTool(), ["sign", "/fd", "SHA256", "/f", Path.Combine(scratch, KeyStore), "/p", Password, .. files]);

    /// <summary>Gives a copied MSIX a test publisher and isolated registration identity.</summary>
    /// <param name="package">The temporary MSIX copy.</param>
    /// <param name="scratch">The fixture directory.</param>
    internal static void SetTestIdentity(string package, string scratch)
    {
        var folder = Path.Combine(scratch, "msix-fixture");
        ZipFile.ExtractToDirectory(package, folder);
        var path = Path.Combine(folder, "AppxManifest.xml");
        var manifest = XDocument.Load(path);
        var ns = manifest.Root!.Name.Namespace;
        var identity = manifest.Root.Element(ns + "Identity")!;
        identity.SetAttributeValue("Name", PackageIdentity);
        identity.SetAttributeValue("Publisher", TestPublisher);
        manifest.Root.Element(ns + "Properties")!.Element(ns + "PublisherDisplayName")!.Value = "Package Check";
        manifest.Save(path);
        File.Delete(Path.Combine(folder, "AppxBlockMap.xml"));
        File.Delete(Path.Combine(folder, "[Content_Types].xml"));
        File.Delete(Path.Combine(folder, "AppxSignature.p7x"));
        MsixWriter.Build(folder, package);
    }

#if WINDOWS
    /// <summary>Installs the replaced packages, upgrades an older MSI, launches each viewer and removes the fixtures, keeping the person's data.</summary>
    /// <param name="msi">The replaced MSI.</param>
    /// <param name="previousMsi">An older MSI built from the same payload, to upgrade from.</param>
    /// <param name="msix">The replaced MSIX.</param>
    /// <param name="scratch">The test certificate directory.</param>
    /// <param name="pdf">The check PDF.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">An installer or cleanup operation fails.</exception>
    internal static async Task CheckAsync(string msi, string previousMsi, string msix, string scratch, string pdf)
    {
        using var certificate = X509CertificateLoader.LoadCertificateFromFile(Path.Combine(scratch, PublicCertificate));
        using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);
        store.Add(certificate);
        var data = UserDataCheck.Seed(Path.Combine(scratch, "user"), pdf);
        Environment.SetEnvironmentVariable(UserDataCheck.ConfigHomeVariable, data.ConfigHome);
        try
        {
            await CheckMsixAsync(msix, pdf).ConfigureAwait(false);
            data.Verify("installing and removing the MSIX");
            await CheckMsiUpgradeAsync(msi, previousMsi, Path.Combine(scratch, "installed-msi"), pdf, data).ConfigureAwait(false);
        }
        finally
        {
            store.Remove(certificate);
        }

        data.Verify("removing the MSI");
    }

    /// <summary>Installs the MSIX, launches its viewer and removes it.</summary>
    /// <param name="msix">The replaced MSIX.</param>
    /// <param name="pdf">The check PDF.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The MSIX cannot be installed.</exception>
    private static async Task CheckMsixAsync(string msix, string pdf)
    {
        var manager = new PackageManager();
        var result = await manager.AddPackageAsync(new(msix), null, DeploymentOptions.None).AsTask().ConfigureAwait(false);
        if (result.ExtendedErrorCode is { } deploymentError)
        {
            throw new InvalidOperationException($"MSIX installation failed: {result.ErrorText}", deploymentError);
        }

        var package = FindInstalledPackage(manager);
        try
        {
            await PackageLaunch.CheckAsync(Path.Combine(package.InstalledLocation.Path, Executable), pdf).ConfigureAwait(false);
        }
        finally
        {
            _ = await manager.RemovePackageAsync(package.Id.FullName).AsTask().ConfigureAwait(false);
        }
    }

    /// <summary>Installs an older MSI, upgrades it to the replaced MSI and removes every fixture version.</summary>
    /// <param name="msi">The replaced MSI.</param>
    /// <param name="previousMsi">The older MSI.</param>
    /// <param name="installed">The install folder.</param>
    /// <param name="pdf">The check PDF.</param>
    /// <param name="data">The seeded user data.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The upgrade or removal fails.</exception>
    private static async Task CheckMsiUpgradeAsync(string msi, string previousMsi, string installed, string pdf, UserDataCheck data)
    {
        _ = NativeMethods.MsiSetInternalUI(SilentUi, IntPtr.Zero);
        uint removal;
        try
        {
            await InstallMsiAsync(previousMsi, installed).ConfigureAwait(false);
            await PackageLaunch.CheckAsync(Path.Combine(installed, Executable), pdf).ConfigureAwait(false);
            data.Verify("installing the older MSI");
            await InstallMsiAsync(msi, installed).ConfigureAwait(false);
            var products = FindInstalledMsiProducts();
            if (products.Count != 1)
            {
                throw new InvalidOperationException($"The MSI upgrade left {products.Count} versions installed.");
            }

            await PackageLaunch.CheckAsync(Path.Combine(installed, Executable), pdf).ConfigureAwait(false);
            data.Verify("upgrading the MSI");
        }
        finally
        {
            removal = RemoveMsiProducts();
        }

        if (removal != 0)
        {
            throw new InvalidOperationException($"Could not uninstall the MSI fixture: {removal}.");
        }

        if (FindInstalledMsiProducts().Count != 0)
        {
            throw new InvalidOperationException("Removing the MSI left the viewer registered.");
        }
    }

    /// <summary>Removes every installed fixture version, including an older one that a failed upgrade left behind.</summary>
    /// <returns>Zero, or the last Windows Installer failure code.</returns>
    private static uint RemoveMsiProducts()
    {
        uint failure = 0;
        foreach (var product in FindInstalledMsiProducts())
        {
            var status = NativeMethods.MsiConfigureProductEx(product, 0, MsiStateAbsent, "REBOOT=ReallySuppress");
            failure = status == 0 ? failure : status;
        }

        return failure;
    }

    /// <summary>Installs an MSI for the current user into a fixed folder, showing the log on failure.</summary>
    /// <param name="msi">The MSI to install.</param>
    /// <param name="folder">The install folder.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">Windows Installer reports a failure.</exception>
    private static async Task InstallMsiAsync(string msi, string folder)
    {
        var log = Path.ChangeExtension(msi, ".log");
        _ = NativeMethods.MsiEnableLog(0x3fff, log, 0);
        var status = NativeMethods.MsiInstallProduct(msi, $"ALLUSERS=2 MSIINSTALLPERUSER=1 REBOOT=ReallySuppress INSTALLFOLDER=\"{folder}\"");
        if (status == 0)
        {
            return;
        }

        Console.WriteLine(await File.ReadAllTextAsync(log).ConfigureAwait(false));
        throw new InvalidOperationException($"Installing {Path.GetFileName(msi)} failed with {status}.");
    }

    /// <summary>Lists the installed products that share the viewer's upgrade code.</summary>
    /// <returns>The product codes.</returns>
    /// <exception cref="InvalidOperationException">Windows Installer cannot list the products.</exception>
    private static List<string> FindInstalledMsiProducts()
    {
        const uint noMoreItems = 259;
        const int productCodeLength = 38;
        List<string> products = [];
        Span<char> buffer = stackalloc char[productCodeLength + 1];
        uint status;
        while ((status = NativeMethods.MsiEnumRelatedProducts(MsiBuilder.UpgradeCode, 0, (uint)products.Count, buffer)) != noMoreItems)
        {
            if (status != 0)
            {
                throw new InvalidOperationException($"Could not list installed MSI products: {status}.");
            }

            products.Add(new(buffer[..productCodeLength]));
        }

        return products;
    }

    /// <summary>Finds the installed MSIX fixture.</summary>
    /// <param name="manager">The package manager.</param>
    /// <returns>The registered test package.</returns>
    /// <exception cref="InvalidOperationException">The fixture was not registered.</exception>
    private static Windows.ApplicationModel.Package FindInstalledPackage(PackageManager manager)
    {
        foreach (var candidate in manager.FindPackagesForUser(string.Empty))
        {
            if (candidate.Id.Name != PackageIdentity)
            {
                continue;
            }

            return candidate;
        }

        throw new InvalidOperationException("MSIX installation did not register the viewer.");
    }
#endif
}
