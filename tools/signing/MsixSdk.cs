// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;

using PdfViewerLite.Tools.Packaging;

using Refit;

namespace PdfViewerLite.Tools.Signing;

/// <summary>Builds the pinned Microsoft MSIX packer for Linux release signing.</summary>
internal static class MsixSdk
{
    /// <summary>SDK revision used by Windows packaging.</summary>
    private const string Revision = "25a65f5c1690930813bcc10cdf1d59fa865f2bb1";

    /// <summary>Maximum concurrent native compilation processes.</summary>
    private const int MaximumBuildProcesses = 8;

    /// <summary>Shared source download client.</summary>
    private static readonly HttpClient Client = new() { BaseAddress = new("https://codeload.github.com") };

    /// <summary>Downloads and builds the packer.</summary>
    /// <param name="scratch">Temporary signing directory.</param>
    /// <returns>The native packer path.</returns>
    internal static async Task<string> BuildAsync(string scratch)
    {
        var folder = Path.Combine(scratch, "msix-sdk");
        _ = Directory.CreateDirectory(folder);
        var api = RestService.ForGenerated<IPackagingSourceApi>(Client);
        var address = new Uri($"https://codeload.github.com/microsoft/msix-packaging/zip/{Revision}");
        Console.WriteLine($"Downloading Microsoft MSIX SDK from {address}.");
        using var response = await api.DownloadAsync(address, CancellationToken.None).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        var archive = Path.Combine(folder, "source.zip");
        await using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
        await using (var target = File.Create(archive))
        {
            await source.CopyToAsync(target).ConfigureAwait(false);
        }

        await ZipFile.ExtractToDirectoryAsync(archive, folder).ConfigureAwait(false);
        var sdk = Path.Combine(folder, $"msix-packaging-{Revision}");
        var build = Path.Combine(folder, "build");

        // The SDK uses ICU's C locale API; ICU's C++ headers require a newer language standard.
        BuildTools.Run("cmake", ["-S", sdk, "-B", build, "-DLINUX=on", "-DMSIX_PACK=on", "-DUSE_VALIDATION_PARSER=on",
            "-DMSIX_TESTS=off", "-DMSIX_SAMPLES=off", "-DCMAKE_BUILD_TYPE=Release", "-DCMAKE_CXX_FLAGS=-DU_SHOW_CPLUSPLUS_API=0"]);
        var processes = Math.Min(Environment.ProcessorCount, MaximumBuildProcesses).ToString(System.Globalization.CultureInfo.InvariantCulture);
        BuildTools.Run("cmake", "--build", build, "--target", "makemsix", "--parallel", processes);
        return Path.Combine(build, "bin", "makemsix");
    }
}
