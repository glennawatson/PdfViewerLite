// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.CommandLine;
using PdfViewerLite.Tools.Commands;

namespace PdfViewerLite.Tools;

/// <summary>Runs packaging, signing and development commands.</summary>
internal static class Program
{
    /// <summary>The artifacts directory argument name.</summary>
    private const string FolderArgument = "folder";

    /// <summary>The manifest argument name.</summary>
    private const string ManifestArgument = "manifest";

    /// <summary>The package argument name.</summary>
    private const string PackageArgument = "package";

    /// <summary>The output argument name.</summary>
    private const string OutputArgument = "output";

    /// <summary>The version argument name.</summary>
    private const string VersionArgument = "version";

    /// <summary>Runs the requested command.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The command exit code.</returns>
    internal static async Task<int> Main(string[] args)
    {
        var root = new RootCommand("PdfViewerLite packaging, signing and development tools.");
        root.Subcommands.Add(CreatePackaging());
        root.Subcommands.Add(CreateSigning());
        root.Subcommands.Add(CreateCi());
        root.Subcommands.Add(CreateAccessibility());
        root.Subcommands.Add(CreateVoiceModels());
        root.Subcommands.Add(CommandFactory.Create("publish", "Publish the Native AOT app.", PublishCommand.Run, "rid", VersionArgument));
        root.Subcommands.Add(CommandFactory.Create("icons", "Create platform icons.", IconsCommand.Run));
        root.Subcommands.Add(CommandFactory.Create("release-notes", "Write release notes.", ReleaseNotesCommand.Run, VersionArgument, "source-sha", OutputArgument));
        return await root.Parse(args).InvokeAsync().ConfigureAwait(false);
    }

    /// <summary>Creates the package commands.</summary>
    /// <returns>The command group.</returns>
    private static Command CreatePackaging() => new(PackageArgument, "Create release packages.")
    {
        CommandFactory.Create("windows", "Create Windows MSI, MSIX and ZIP packages.", WindowsCommand.Run, "rid", VersionArgument),
        CommandFactory.Create("linux", "Create Linux AppImage, DEB, RPM and tar.gz packages.", LinuxCommand.RunAsync, "rid", VersionArgument),
        CommandFactory.Create("macos", "Create macOS DMG and app ZIP packages.", MacosCommand.Run, "rid", VersionArgument),
    };

    /// <summary>Creates the signing commands.</summary>
    /// <returns>The command group.</returns>
    private static Command CreateSigning() => new("sign", "Sign and verify release assets.")
    {
        CommandFactory.Create("authenticode", "Sign and verify Windows payloads and installers.", SignAuthenticodeCommand.RunAsync),
        CommandFactory.Create("detached", "Sign and verify detached CMS signatures.", SignDetachedCommand.Run, "certificate-source"),
        CommandFactory.Create("verify", "Verify installer signatures and certificate pins.", VerifyAllCommand.RunAsync, FolderArgument),
        CommandFactory.Create("verify-msix", "Verify an MSIX package.", VerifyMsixCommand.RunAsync, PackageArgument),
        CommandFactory.Create("verify-msi", "Verify an MSI package.", VerifyMsiCommand.Run, PackageArgument),
    };

    /// <summary>Creates the CI preparation commands.</summary>
    /// <returns>The command group.</returns>
    private static Command CreateCi() => new("ci", "Prepare and verify CI builds.")
    {
        CommandFactory.Create("prepare", "Install platform prerequisites.", PrepareCommand.Run, "mode"),
        CommandFactory.Create("build-tools", "Build repository tools.", BuildToolsCommand.Run),
        CommandFactory.Create("check-windows-packages", "Check installer formats after payload replacement.", CheckWindowsPackagesCommand.Run, FolderArgument),
    };

    /// <summary>Creates accessibility commands.</summary>
    /// <returns>The command group.</returns>
    private static Command CreateAccessibility() => new("accessibility", "Check screen-reader access.")
    {
        CommandFactory.Create("create-check-pdf", "Create a screen-reader test document.", CreateCheckPdfCommand.RunAsync, OutputArgument),
        CommandFactory.Create("atspi-walk", "Inspect accessible controls.", AtspiWalkCommand.RunAsync, "address", "application", "minimum-controls"),
    };

    /// <summary>Creates voice model commands.</summary>
    /// <returns>The command group.</returns>
    private static Command CreateVoiceModels() => new("voice", "Prepare and verify voice models.")
    {
        CommandFactory.Create("import-melo", "Import Melo voice assets.", ImportMeloCommand.RunAsync, ManifestArgument, OutputArgument, "local-source?"),
        CommandFactory.Create("check-melo", "Check Melo against its reference.", CheckMeloCommand.RunAsync, ManifestArgument, "voices", "reference"),
        CommandFactory.Create("reference-melo", "Create a checked native Melo snapshot.", ReferenceMeloCommand.RunAsync, "voices", "reference", OutputArgument),
        CommandFactory.Create("mirror-kokoro", "Download Kokoro voice assets.", MirrorKokoroCommand.RunAsync, OutputArgument),
        CommandFactory.Create(ManifestArgument, "Write voice asset hashes.", ManifestCommand.RunAsync, "folder"),
        CommandFactory.Create("release", "Check or publish voice releases.", ReleaseCommand.RunAsync, "mode", "tag", "destination"),
    };
}
