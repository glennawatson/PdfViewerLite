// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Seeds a person's preferences and a document, then checks that installers leave both intact.</summary>
internal sealed class UserDataCheck
{
    /// <summary>The seeded signature name preference.</summary>
    internal const string SignatureName = "Package Check";

    /// <summary>The seeded focus text width preference.</summary>
    internal const int FocusTextWidth = 58;

    /// <summary>The environment variable the viewer reads its configuration folder from.</summary>
    internal const string ConfigHomeVariable = "XDG_CONFIG_HOME";

    /// <summary>The configuration folder below the seeded root.</summary>
    internal const string ConfigFolder = "config";

    /// <summary>The settings file below the configuration folder.</summary>
    private const string SettingsFile = "pdfviewerlite/settings.json";

    /// <summary>The seeded document below the seeded root.</summary>
    private const string DocumentFile = "Documents/package-check-notes.pdf";

    /// <summary>The JSON name of the signature preference.</summary>
    private const string SignatureNameProperty = "signatureName";

    /// <summary>The JSON name of the focus text width preference.</summary>
    private const string FocusTextWidthProperty = "focusTextWidth";

    /// <summary>The SHA-256 hash of the seeded document.</summary>
    private readonly byte[] _documentHash;

    /// <summary>Initializes a new instance of the <see cref="UserDataCheck"/> class.</summary>
    /// <param name="root">The seeded root folder.</param>
    /// <param name="documentHash">The seeded document hash.</param>
    private UserDataCheck(string root, byte[] documentHash)
    {
        Root = root;
        _documentHash = documentHash;
    }

    /// <summary>Gets the seeded root folder.</summary>
    internal string Root { get; }

    /// <summary>Gets the configuration folder to give the viewer.</summary>
    internal string ConfigHome => Path.Combine(Root, ConfigFolder);

    /// <summary>Writes the preferences and copies the document below a new root folder.</summary>
    /// <param name="root">The folder to seed; it must not already hold seeded data.</param>
    /// <param name="document">The PDF to keep as the person's document.</param>
    /// <returns>The check for the seeded data.</returns>
    internal static UserDataCheck Seed(string root, string document)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentException.ThrowIfNullOrEmpty(document);
        var settings = Path.Combine(root, ConfigFolder, SettingsFile);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllBytes(settings, CreateSettings());
        var copy = Path.Combine(root, DocumentFile);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(document, copy);
        return new(root, SHA256.HashData(File.ReadAllBytes(copy)));
    }

    /// <summary>Creates the seeded settings file contents.</summary>
    /// <returns>The UTF-8 JSON settings.</returns>
    internal static byte[] CreateSettings()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new() { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString(SignatureNameProperty, SignatureName);
            writer.WriteNumber(FocusTextWidthProperty, FocusTextWidth);
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    /// <summary>Checks that the seeded data under the original root is unchanged.</summary>
    /// <param name="stage">The install stage, for the failure message.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Verify(string stage) => VerifyCopy(Root, stage);

    /// <summary>Checks seeded data copied back from another machine or container.</summary>
    /// <param name="root">The root holding the same layout as the seeded root.</param>
    /// <param name="stage">The install stage, for the failure message.</param>
    /// <exception cref="InvalidOperationException">A preference or the document is missing or changed.</exception>
    internal void VerifyCopy(string root, string stage)
    {
        var settings = Path.Combine(root, ConfigFolder, SettingsFile);
        if (!File.Exists(settings))
        {
            throw new InvalidOperationException($"The settings file was removed after {stage}.");
        }

        // The viewer may rewrite other settings when it runs, so only the seeded preferences must survive.
        using (var json = JsonDocument.Parse(File.ReadAllBytes(settings)))
        {
            var preferences = json.RootElement;
            if (!preferences.TryGetProperty(SignatureNameProperty, out var name) || name.GetString() != SignatureName
                || !preferences.TryGetProperty(FocusTextWidthProperty, out var width) || width.GetInt32() != FocusTextWidth)
            {
                throw new InvalidOperationException($"The saved preferences changed after {stage}.");
            }
        }

        var document = Path.Combine(root, DocumentFile);
        if (!File.Exists(document) || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(File.ReadAllBytes(document)), _documentHash))
        {
            throw new InvalidOperationException($"The person's document was removed or changed after {stage}.");
        }

        Console.WriteLine($"User preferences and documents preserved after {stage}.");
    }
}
