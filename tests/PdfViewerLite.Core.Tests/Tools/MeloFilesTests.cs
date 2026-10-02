// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text.Json;
using PdfViewerLite.Tools.VoiceModels;

namespace PdfViewerLite.Core.Tests.Tools;

/// <summary>Checks source pins and rejects files that cannot be safely imported.</summary>
public sealed class MeloFilesTests : IDisposable
{
    /// <summary>A small asset with a known size and hash.</summary>
    private const string Content = "abc";

    /// <summary>The name of the asset.</summary>
    private const string AssetName = "voice.bin";

    /// <summary>The folder owned by one test.</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"melo-files-{Guid.NewGuid():N}");

    /// <summary>Initializes a new instance of the <see cref="MeloFilesTests"/> class.</summary>
    public MeloFilesTests() => _ = Directory.CreateDirectory(_directory);

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_directory, true);

    /// <summary>Loads the source address, asset size and hash from the manifest.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsPinnedAsset()
    {
        var path = WriteManifest([AssetName], Content.Length, Digest());

        var files = MeloFiles.Load(path);

        await Assert.That(files.Count).IsEqualTo(1);
        await Assert.That(files[0].LocalName).IsEqualTo(AssetName);
        await Assert.That(files[0].Source).IsEqualTo(new("https://example.invalid/voice.bin"));
        await Assert.That(files[0].Bytes).IsEqualTo(Content.Length);
        await Assert.That(files[0].Sha256).IsEqualTo(Digest());
    }

    /// <summary>Rejects paths before any file can be written outside the staging folder.</summary>
    /// <param name="name">An unsafe asset name.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("../escape.bin")]
    [Arguments("..\\escape.bin")]
    [Arguments("/escape.bin")]
    [Arguments("C:\\escape.bin")]
    [Arguments("..")]
    [Arguments(".")]
    [Arguments("")]
    [Arguments(" ")]
    public async Task RejectsUnsafeAssetNames(string name)
    {
        var path = WriteManifest([name], Content.Length, Digest());

        await Assert.That(() => MeloFiles.Load(path)).Throws<InvalidDataException>();
    }

    /// <summary>Rejects names that would overwrite each other on Windows.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsDuplicateAssetNames()
    {
        var path = WriteManifest([AssetName, AssetName.ToUpperInvariant()], Content.Length, Digest());

        await Assert.That(() => MeloFiles.Load(path)).Throws<InvalidDataException>();
    }

    /// <summary>Rejects a manifest with no assets.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsEmptyManifest()
    {
        var path = WriteManifest([], Content.Length, Digest());

        await Assert.That(() => MeloFiles.Load(path)).Throws<InvalidDataException>();
    }

    /// <summary>Rejects a hash that is not a SHA-256 digest.</summary>
    /// <param name="digest">The malformed digest.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("")]
    [Arguments("invalid")]
    [Arguments("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public async Task RejectsInvalidDigest(string digest)
    {
        var path = WriteManifest([AssetName], Content.Length, digest);

        await Assert.That(() => MeloFiles.Load(path)).Throws<InvalidDataException>();
    }

    /// <summary>Rejects an asset size that cannot hold a model file.</summary>
    /// <param name="bytes">The invalid byte count.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0L)]
    [Arguments(-1L)]
    public async Task RejectsInvalidSize(long bytes)
    {
        var path = WriteManifest([AssetName], bytes, Digest());

        await Assert.That(() => MeloFiles.Load(path)).Throws<InvalidDataException>();
    }

    /// <summary>Accepts an intact file without changing its contents.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AcceptsPinnedFile()
    {
        var path = WriteManifest([AssetName], Content.Length, Digest());
        await File.WriteAllTextAsync(Path.Combine(_directory, AssetName), Content);

        await Assert.That(() => MeloFiles.ValidateAsync(MeloFiles.Load(path), _directory)).ThrowsNothing();
        await Assert.That(await File.ReadAllTextAsync(Path.Combine(_directory, AssetName))).IsEqualTo(Content);
    }

    /// <summary>Rejects damaged files even when their size matches the source.</summary>
    /// <param name="content">The damaged file contents.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("bad")]
    [Arguments("longer")]
    public async Task RejectsDamagedFile(string content)
    {
        var path = WriteManifest([AssetName], Content.Length, Digest());
        await File.WriteAllTextAsync(Path.Combine(_directory, AssetName), content);

        await Assert.That(() => MeloFiles.ValidateAsync(MeloFiles.Load(path), _directory)).Throws<InvalidDataException>();
    }

    /// <summary>Computes the expected hash of the small test asset.</summary>
    /// <returns>The lowercase hexadecimal digest.</returns>
    private static string Digest() => Convert.ToHexStringLower(SHA256.HashData("abc"u8));

    /// <summary>Writes a source manifest without reflection-based serialization.</summary>
    /// <param name="names">The asset names.</param>
    /// <param name="bytes">The size to pin.</param>
    /// <param name="digest">The hash to pin.</param>
    /// <returns>The manifest path.</returns>
    private string WriteManifest(string[] names, long bytes, string digest)
    {
        var path = Path.Combine(_directory, "source.json");
        using var output = File.Create(path);
        using var writer = new Utf8JsonWriter(output);
        writer.WriteStartObject();
        writer.WriteString("source", "https://example.invalid/");
        writer.WriteStartArray("files");
        foreach (var name in names)
        {
            writer.WriteStartObject();
            writer.WriteString(nameof(name), name);
            writer.WriteNumber(nameof(bytes), bytes);
            writer.WriteString("sha256", digest);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        return path;
    }
}
