// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace PdfViewerLite.Tools.Packaging;

/// <summary>Generates a Windows Installer database using native Windows APIs.</summary>
internal static partial class MsiBuilder
{
    /// <summary>The upgrade code shared by every release, so a newer MSI replaces an older one.</summary>
    internal const string UpgradeCode = "{0C547842-7023-455E-9807-E4C0CA332DA8}";

    /// <summary>Bit flag indicating a 64-bit component.</summary>
    private const int ComponentAttribute64Bit = 256;

    /// <summary>Bit flag indicating feature files are installed locally.</summary>
    private const int FeatureAttributeFavorLocal = 2;

    /// <summary>Upgrade attributes that migrate features from versions below the maximum.</summary>
    private const int UpgradeAttributeVersionMaxInclusive = 257;

    /// <summary>Upgrade attributes that only detect the same or a newer version.</summary>
    private const int UpgradeAttributeVersionMinInclusive = 258;

    /// <summary>Default feature display hierarchy level.</summary>
    private const int DefaultDisplayLevel = 1;

    /// <summary>Default feature install level.</summary>
    private const int DefaultInstallLevel = 1;

    /// <summary>Window show command for normal display.</summary>
    private const int NormalShowCommand = 1;

    /// <summary>Default icon index within the icon file.</summary>
    private const int DefaultIconIndex = 0;

    /// <summary>Default disk identifier for source media.</summary>
    private const int DefaultDiskId = 1;

    /// <summary>Byte length of a GUID structure.</summary>
    private const int GuidByteLength = 16;

    /// <summary>ANSI code page for Windows-1252.</summary>
    private const int DefaultCodepage = 1252;

    /// <summary>Minimum Windows Installer version requirement.</summary>
    private const int MinimumInstallerVersion = 200;

    /// <summary>Summary information word count for compressed packages.</summary>
    private const int CompressedSourceWordCount = 2;

    /// <summary>Record field index for stream data.</summary>
    private const uint StreamRecordField = 1;

    /// <summary>Parameter count for stream record.</summary>
    private const uint StreamRecordParamCount = 1;

    /// <summary>Maximum summary information properties allocated.</summary>
    private const uint MaxSummaryProperties = 20;

    /// <summary>Property ID for code page in summary info.</summary>
    private const uint PidCodepage = 1;

    /// <summary>Property ID for title in summary info.</summary>
    private const uint PidTitle = 2;

    /// <summary>Property ID for subject in summary info.</summary>
    private const uint PidSubject = 3;

    /// <summary>Property ID for author in summary info.</summary>
    private const uint PidAuthor = 4;

    /// <summary>Property ID for keywords in summary info.</summary>
    private const uint PidKeywords = 5;

    /// <summary>Property ID for comments in summary info.</summary>
    private const uint PidComments = 6;

    /// <summary>Property ID for template platform in summary info.</summary>
    private const uint PidTemplate = 7;

    /// <summary>Property ID for package revision GUID in summary info.</summary>
    private const uint PidRevNumber = 9;

    /// <summary>Property ID for required installer version in summary info.</summary>
    private const uint PidPageCount = 14;

    /// <summary>Property ID for word count flags in summary info.</summary>
    private const uint PidWordCount = 15;

    /// <summary>Property ID for creating application in summary info.</summary>
    private const uint PidAppName = 18;

    /// <summary>Variant type for 16-bit signed integer.</summary>
    private const uint VtI2 = 2;

    /// <summary>Variant type for 32-bit signed integer.</summary>
    private const uint VtI4 = 3;

    /// <summary>Variant type for null-terminated string.</summary>
    private const uint VtLpStr = 30;

    /// <summary>Sequence number for FindRelatedProducts action.</summary>
    private const int SeqFindRelatedProducts = 200;

    /// <summary>Sequence number for AppSearch action.</summary>
    private const int SeqAppSearch = 400;

    /// <summary>Sequence number for ValidateProductID action.</summary>
    private const int SeqValidateProductId = 700;

    /// <summary>Sequence number for CostInitialize action.</summary>
    private const int SeqCostInitialize = 800;

    /// <summary>Sequence number for FileCost action.</summary>
    private const int SeqFileCost = 900;

    /// <summary>Sequence number for CostFinalize action.</summary>
    private const int SeqCostFinalize = 1000;

    /// <summary>Sequence number for ExecuteAction action.</summary>
    private const int SeqExecuteAction = 1300;

    /// <summary>Sequence number for InstallValidate action.</summary>
    private const int SeqInstallValidate = 1400;

    /// <summary>Sequence number for InstallInitialize action.</summary>
    private const int SeqInstallInitialize = 1500;

    /// <summary>Sequence number for ProcessComponents action.</summary>
    private const int SeqProcessComponents = 1600;

    /// <summary>Sequence number for UnpublishFeatures action.</summary>
    private const int SeqUnpublishFeatures = 1800;

    /// <summary>Sequence number for RemoveExistingProducts, between InstallValidate and InstallInitialize.</summary>
    private const int SeqRemoveExistingProducts = 1450;

    /// <summary>Sequence number for RemoveShortcuts action.</summary>
    private const int SeqRemoveShortcuts = 3200;

    /// <summary>Sequence number for RemoveFiles action.</summary>
    private const int SeqRemoveFiles = 3500;

    /// <summary>Sequence number for InstallFiles action.</summary>
    private const int SeqInstallFiles = 4000;

    /// <summary>Sequence number for CreateShortcuts action.</summary>
    private const int SeqCreateShortcuts = 4500;

    /// <summary>Sequence number for RegisterUser action.</summary>
    private const int SeqRegisterUser = 6000;

    /// <summary>Sequence number for RegisterProduct action.</summary>
    private const int SeqRegisterProduct = 6100;

    /// <summary>Sequence number for PublishFeatures action.</summary>
    private const int SeqPublishFeatures = 6300;

    /// <summary>Sequence number for PublishProduct action.</summary>
    private const int SeqPublishProduct = 6400;

    /// <summary>Sequence number for InstallFinalize action.</summary>
    private const int SeqInstallFinalize = 6600;

    /// <summary>License file name.</summary>
    private const string LicenseFileName = "LICENSE";

    /// <summary>CostInitialize standard action name.</summary>
    private const string ActionCostInitialize = "CostInitialize";

    /// <summary>FileCost standard action name.</summary>
    private const string ActionFileCost = "FileCost";

    /// <summary>CostFinalize standard action name.</summary>
    private const string ActionCostFinalize = "CostFinalize";

    /// <summary>MSI database open mode for direct creation.</summary>
    private static readonly IntPtr MsiOpenDatabaseModeCreateDirect = (IntPtr)4;

    /// <summary>Builds a standalone Windows Installer package for the published app.</summary>
    /// <param name="source">Directory containing the published binaries.</param>
    /// <param name="msiPath">Destination path for the .msi package.</param>
    /// <param name="version">Application version string.</param>
    /// <param name="rid">Runtime identifier (win-x64 or win-arm64).</param>
    /// <param name="root">Repository root directory.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="FileNotFoundException">A required input or output file is missing.</exception>
    /// <exception cref="InvalidOperationException">An MSI API call fails.</exception>
    internal static void Build(string source, string msiPath, string version, string rid, string root)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(msiPath);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(rid);
        ArgumentNullException.ThrowIfNull(root);

        if (File.Exists(msiPath))
        {
            File.Delete(msiPath);
        }

        var artifactsDir = Path.GetDirectoryName(msiPath)!;
        var cabStaging = Path.Combine(artifactsDir, $"msi-cab-{rid}");
        var (fileEntries, cabPath) = PrepareCabinet(source, cabStaging, root);

        CheckStatus(NativeMethods.MsiOpenDatabase(msiPath, MsiOpenDatabaseModeCreateDirect, out var database), "MsiOpenDatabase");
        try
        {
            PopulateDatabase(database, fileEntries, cabPath, root, version, rid);
            CheckStatus(NativeMethods.MsiDatabaseCommit(database), "MsiDatabaseCommit");
        }
        finally
        {
            _ = NativeMethods.MsiCloseHandle(database);
        }
    }

    /// <summary>Prepares the cabinet file and file metadata.</summary>
    /// <param name="source">Directory containing the published binaries.</param>
    /// <param name="cabStaging">Temporary directory for building the CAB.</param>
    /// <param name="root">Repository root directory.</param>
    /// <returns>List of file entries and path to the generated cabinet.</returns>
    /// <exception cref="FileNotFoundException">The cabinet was not created.</exception>
    private static (List<(string FileKey, string SourcePath, string RelativePath, string FileName, long FileSize)> Entries, string CabPath) PrepareCabinet(
        string source,
        string cabStaging,
        string root)
    {
        if (Directory.Exists(cabStaging))
        {
            Directory.Delete(cabStaging, true);
        }

        _ = Directory.CreateDirectory(cabStaging);
        var allFiles = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
        Array.Sort(allFiles, StringComparer.OrdinalIgnoreCase);
        var licenseFile = Path.Combine(root, LicenseFileName);
        var hasLicense = File.Exists(licenseFile);
        var entries = new List<(string FileKey, string SourcePath, string RelativePath, string FileName, long FileSize)>();
        const string exeFileKey = "fil_pdfviewerlite.exe";
        for (var index = 0; index < allFiles.Length; index++)
        {
            var filePath = allFiles[index];
            var relativePath = Path.GetRelativePath(source, filePath);
            var fileName = Path.GetFileName(filePath);
            var fileKey = string.Equals(fileName, "pdfviewerlite.exe", StringComparison.OrdinalIgnoreCase) ? exeFileKey : $"fil_{index}";
            var info = new FileInfo(filePath);
            entries.Add((fileKey, filePath, relativePath, fileName, info.Length));
        }

        if (hasLicense)
        {
            var info = new FileInfo(licenseFile);
            entries.Add(("fil_license", licenseFile, LicenseFileName, LicenseFileName, info.Length));
        }

        var cabinetFiles = new List<(string Name, string Path)>(entries.Count);
        foreach (var entry in entries)
        {
            cabinetFiles.Add((entry.FileKey, entry.SourcePath));
        }

        CabinetWriter.Build(cabinetFiles, Path.Combine(cabStaging, "app.cab"));
        var cabPath = Path.Combine(cabStaging, "app.cab");
        if (!File.Exists(cabPath))
        {
            throw new FileNotFoundException("The cabinet writer did not produce app.cab.");
        }

        return (entries, cabPath);
    }

    /// <summary>Populates all tables and streams in the MSI database.</summary>
    /// <param name="database">Database handle.</param>
    /// <param name="entries">File entries.</param>
    /// <param name="cabPath">Path to cabinet file.</param>
    /// <param name="root">Repository root directory.</param>
    /// <param name="version">Application version.</param>
    /// <param name="rid">Runtime identifier.</param>
    /// <exception cref="InvalidOperationException">An MSI operation fails.</exception>
    private static void PopulateDatabase(
        IntPtr database,
        List<(string FileKey, string SourcePath, string RelativePath, string FileName, long FileSize)> entries,
        string cabPath,
        string root,
        string version,
        string rid)
    {
        const string exeFileKey = "fil_pdfviewerlite.exe";
        const string exeCompKey = "cmp_pdfviewerlite.exe";
        var iconPath = Path.Combine(root, "packaging/windows/PdfViewerLite.ico");

        CreateSchema(database);
        InsertStreams(database, cabPath, iconPath);
        InsertDirectories(database, entries);
        InsertComponentsAndFiles(database, entries, exeFileKey, exeCompKey);
        InsertFeatures(database, entries, exeCompKey);
        InsertShortcuts(database, exeCompKey, exeFileKey);
        InsertProperties(database, version);
        InsertSequences(database, version);
        SetSummaryInformation(database, version, rid);
    }

    /// <summary>Creates standard MSI database tables.</summary>
    /// <param name="database">Database handle.</param>
    /// <exception cref="InvalidOperationException">A table creation query fails.</exception>
    private static void CreateSchema(IntPtr database)
    {
        string[] tableDefinitions =
        [
            "Directory (Directory CHAR(72) NOT NULL, Directory_Parent CHAR(72), DefaultDir CHAR(255) NOT NULL PRIMARY KEY Directory)",
            "Component (Component CHAR(72) NOT NULL, ComponentId CHAR(38), Directory_ CHAR(72) NOT NULL, Attributes INTEGER NOT NULL, Condition CHAR(255), KeyPath CHAR(72) PRIMARY KEY Component)",
            "File (File CHAR(72) NOT NULL, Component_ CHAR(72) NOT NULL, FileName CHAR(255) NOT NULL, FileSize LONG NOT NULL, "
                + "Version CHAR(72), Language CHAR(20), Attributes INTEGER, Sequence INTEGER NOT NULL PRIMARY KEY File)",
            "Feature (Feature CHAR(38) NOT NULL, Feature_Parent CHAR(38), Title CHAR(64), Description CHAR(255), Display INTEGER, "
                + "Level INTEGER NOT NULL, Directory_ CHAR(72), Attributes INTEGER NOT NULL PRIMARY KEY Feature)",
            "FeatureComponents (Feature_ CHAR(38) NOT NULL, Component_ CHAR(72) NOT NULL PRIMARY KEY Feature_, Component_)",
            "Media (DiskId INTEGER NOT NULL, LastSequence INTEGER NOT NULL, DiskPrompt CHAR(64), Cabinet CHAR(64), VolumeLabel CHAR(32), Source CHAR(72) PRIMARY KEY DiskId)",
            "Property (Property CHAR(72) NOT NULL, Value CHAR(0) NOT NULL PRIMARY KEY Property)",
            "Icon (Name CHAR(72) NOT NULL, Data OBJECT NOT NULL PRIMARY KEY Name)",
            "Shortcut (Shortcut CHAR(72) NOT NULL, Directory_ CHAR(72) NOT NULL, Name CHAR(255) NOT NULL, Component_ CHAR(72) NOT NULL, "
                + "Target CHAR(72) NOT NULL, Arguments CHAR(255), Description CHAR(255), Hotkey INTEGER, Icon_ CHAR(72), IconIndex INTEGER, ShowCmd INTEGER, WkDir CHAR(72) PRIMARY KEY Shortcut)",
            "CreateFolder (Directory_ CHAR(72) NOT NULL, Component_ CHAR(72) NOT NULL PRIMARY KEY Directory_, Component_)",
            "Upgrade (UpgradeCode CHAR(38) NOT NULL, VersionMin CHAR(20), VersionMax CHAR(20), Language CHAR(255), Attributes INTEGER NOT NULL, "
                + "Remove CHAR(255), ActionProperty CHAR(72) NOT NULL PRIMARY KEY UpgradeCode, VersionMin, VersionMax, Language, Attributes)",
            "InstallExecuteSequence (Action CHAR(72) NOT NULL, Condition CHAR(255), Sequence INTEGER PRIMARY KEY Action)",
            "InstallUISequence (Action CHAR(72) NOT NULL, Condition CHAR(255), Sequence INTEGER PRIMARY KEY Action)",
            "AdminExecuteSequence (Action CHAR(72) NOT NULL, Condition CHAR(255), Sequence INTEGER PRIMARY KEY Action)",
            "AdvtExecuteSequence (Action CHAR(72) NOT NULL, Condition CHAR(255), Sequence INTEGER PRIMARY KEY Action)",
        ];

        for (var index = 0; index < tableDefinitions.Length; index++)
        {
            Execute(database, $"CREATE TABLE {tableDefinitions[index]}");
        }
    }

    /// <summary>Inserts embedded cabinet and icon streams.</summary>
    /// <param name="database">Database handle.</param>
    /// <param name="cabPath">Path to the cabinet file.</param>
    /// <param name="iconPath">Path to the application icon.</param>
    /// <exception cref="InvalidOperationException">Stream insertion fails.</exception>
    private static void InsertStreams(IntPtr database, string cabPath, string iconPath)
    {
        ExecuteWithStream(database, "INSERT INTO _Streams (Name, Data) VALUES ('app.cab', ?)", cabPath);
        if (File.Exists(iconPath))
        {
            ExecuteWithStream(database, "INSERT INTO Icon (Name, Data) VALUES ('AppIcon.ico', ?)", iconPath);
        }
    }

    /// <summary>Collects sorted unique subdirectories from file entries.</summary>
    /// <param name="entries">File entries.</param>
    /// <returns>List of relative subdirectories sorted by length.</returns>
    private static List<string> CollectSubdirectories(List<(string FileKey, string SourcePath, string RelativePath, string FileName, long FileSize)> entries)
    {
        var subdirSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < entries.Count; index++)
        {
            var dir = Path.GetDirectoryName(entries[index].RelativePath);
            if (!string.IsNullOrEmpty(dir))
            {
                _ = subdirSet.Add(dir);
            }
        }

        var subdirs = new List<string>(subdirSet);
        subdirs.Sort(static (a, b) => a.Length.CompareTo(b.Length));
        return subdirs;
    }

    /// <summary>Inserts directory hierarchy rows.</summary>
    /// <param name="database">Database handle.</param>
    /// <param name="entries">File entries.</param>
    /// <exception cref="InvalidOperationException">Directory row insertion fails.</exception>
    private static void InsertDirectories(IntPtr database, List<(string FileKey, string SourcePath, string RelativePath, string FileName, long FileSize)> entries)
    {
        Execute(database, "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('TARGETDIR', '', 'SourceDir')");
        Execute(database, "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('ProgramFiles64Folder', 'TARGETDIR', '.')");
        Execute(database, "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('ManufacturerFolder', 'ProgramFiles64Folder', 'Glenn Watson')");
        Execute(database, "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('INSTALLFOLDER', 'ManufacturerFolder', 'Hyper PDF Viewer')");
        Execute(database, "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('ProgramMenuFolder', 'TARGETDIR', '.')");
        Execute(database, "INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('AppMenuFolder', 'ProgramMenuFolder', 'Hyper PDF Viewer')");

        var subdirs = CollectSubdirectories(entries);
        var dirMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < subdirs.Count; index++)
        {
            var subdir = subdirs[index];
            var dirKey = $"dir_{index}";
            dirMap[subdir] = dirKey;

            var parent = Path.GetDirectoryName(subdir);
            var parentKey = string.IsNullOrEmpty(parent) ? "INSTALLFOLDER" : dirMap[parent];
            var dirName = Path.GetFileName(subdir);

            Execute(database, $"INSERT INTO Directory (Directory, Directory_Parent, DefaultDir) VALUES ('{dirKey}', '{parentKey}', '{EscapeSql(dirName)}')");
        }
    }

    /// <summary>Inserts component and file records.</summary>
    /// <param name="database">Database handle.</param>
    /// <param name="entries">File entries.</param>
    /// <param name="exeFileKey">Identifier of the main executable file.</param>
    /// <param name="exeCompKey">Identifier of the main executable component.</param>
    /// <exception cref="InvalidOperationException">Record insertion fails.</exception>
    private static void InsertComponentsAndFiles(
        IntPtr database,
        List<(string FileKey, string SourcePath, string RelativePath, string FileName, long FileSize)> entries,
        string exeFileKey,
        string exeCompKey)
    {
        var subdirs = CollectSubdirectories(entries);
        var dirMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < subdirs.Count; index++)
        {
            dirMap[subdirs[index]] = $"dir_{index}";
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var compKey = entry.FileKey == exeFileKey ? exeCompKey : $"cmp_{index}";
            var compGuid = CreateDeterministicGuid(entry.RelativePath).ToString("B").ToUpperInvariant();

            var relDir = Path.GetDirectoryName(entry.RelativePath);
            var dirKey = string.IsNullOrEmpty(relDir) ? "INSTALLFOLDER" : dirMap[relDir];

            var compSql = "INSERT INTO Component (Component, ComponentId, Directory_, Attributes, Condition, KeyPath) "
                + $"VALUES ('{compKey}', '{compGuid}', '{dirKey}', {ComponentAttribute64Bit}, '', '{entry.FileKey}')";
            Execute(database, compSql);

            var fileSql = "INSERT INTO File (File, Component_, FileName, FileSize, Version, Language, Attributes, Sequence) "
                + $"VALUES ('{entry.FileKey}', '{compKey}', '{EscapeSql(entry.FileName)}', {entry.FileSize}, '', '', 0, {index + 1})";
            Execute(database, fileSql);
        }

        var mediaSql = $"INSERT INTO Media (DiskId, LastSequence, DiskPrompt, Cabinet, VolumeLabel, Source) VALUES ({DefaultDiskId}, {entries.Count}, '1', '#app.cab', '', '')";
        Execute(database, mediaSql);
    }

    /// <summary>Inserts feature records.</summary>
    /// <param name="database">Database handle.</param>
    /// <param name="entries">File entries.</param>
    /// <param name="exeCompKey">Executable component key.</param>
    /// <exception cref="InvalidOperationException">Feature insertion fails.</exception>
    private static void InsertFeatures(
        IntPtr database,
        List<(string FileKey, string SourcePath, string RelativePath, string FileName, long FileSize)> entries,
        string exeCompKey)
    {
        var featureSql = "INSERT INTO Feature (Feature, Feature_Parent, Title, Description, Display, Level, Directory_, Attributes) "
            + $"VALUES ('MainFeature', '', 'Hyper PDF Viewer', 'Hyper PDF Viewer Application', {DefaultDisplayLevel}, {DefaultInstallLevel}, 'INSTALLFOLDER', {FeatureAttributeFavorLocal})";
        Execute(database, featureSql);

        for (var index = 0; index < entries.Count; index++)
        {
            var compKey = entries[index].FileKey == "fil_pdfviewerlite.exe" ? exeCompKey : $"cmp_{index}";
            Execute(database, $"INSERT INTO FeatureComponents (Feature_, Component_) VALUES ('MainFeature', '{compKey}')");
        }
    }

    /// <summary>Inserts shortcut and folder creation records.</summary>
    /// <param name="database">Database handle.</param>
    /// <param name="exeCompKey">Executable component key.</param>
    /// <param name="exeFileKey">Executable file key.</param>
    /// <exception cref="InvalidOperationException">Shortcut insertion fails.</exception>
    private static void InsertShortcuts(IntPtr database, string exeCompKey, string exeFileKey)
    {
        var shortcutSql = "INSERT INTO Shortcut (Shortcut, Directory_, Name, Component_, Target, Arguments, Description, "
            + "Hotkey, Icon_, IconIndex, ShowCmd, WkDir) VALUES ('AppShortcut', 'AppMenuFolder', 'Hyper PDF Viewer', "
            + $"'{exeCompKey}', '[#{exeFileKey}]', '', 'Hyper PDF Viewer', 0, 'AppIcon.ico', {DefaultIconIndex}, {NormalShowCommand}, 'INSTALLFOLDER')";
        Execute(database, shortcutSql);
        Execute(database, $"INSERT INTO CreateFolder (Directory_, Component_) VALUES ('AppMenuFolder', '{exeCompKey}')");
    }

    /// <summary>Inserts standard property table rows.</summary>
    /// <param name="database">Database handle.</param>
    /// <param name="version">Application version string.</param>
    /// <exception cref="InvalidOperationException">Property insertion fails.</exception>
    private static void InsertProperties(IntPtr database, string version)
    {
        var numeric = Version.Parse(version.Split('-', '+')[0]);
        var msiVersion = $"{numeric.Major}.{numeric.Minor}.{numeric.Build}";
        var productCode = CreateDeterministicGuid($"ProductCode-{msiVersion}").ToString("B").ToUpperInvariant();

        Execute(database, $"INSERT INTO Property (Property, Value) VALUES ('ProductCode', '{productCode}')");
        Execute(database, "INSERT INTO Property (Property, Value) VALUES ('ProductName', 'Hyper PDF Viewer')");
        Execute(database, $"INSERT INTO Property (Property, Value) VALUES ('ProductVersion', '{msiVersion}')");
        Execute(database, "INSERT INTO Property (Property, Value) VALUES ('Manufacturer', 'Glenn Watson')");
        Execute(database, "INSERT INTO Property (Property, Value) VALUES ('ProductLanguage', '1033')");
        Execute(database, $"INSERT INTO Property (Property, Value) VALUES ('UpgradeCode', '{UpgradeCode}')");
        Execute(database, "INSERT INTO Property (Property, Value) VALUES ('ALLUSERS', '1')");
        Execute(database, "INSERT INTO Property (Property, Value) VALUES ('ARPPRODUCTICON', 'AppIcon.ico')");
        Execute(database, "INSERT INTO Property (Property, Value) VALUES ('ARPHELPLINK', 'https://github.com/glennawatson/PdfViewerLite')");
        Execute(database, "INSERT INTO Property (Property, Value) VALUES ('SecureCustomProperties', 'NEWERPRODUCTFOUND;PREVIOUSVERSIONSINSTALLED')");
    }

    /// <summary>Inserts sequence table rows for installation.</summary>
    /// <param name="database">Database handle.</param>
    /// <param name="version">Application version string.</param>
    /// <exception cref="InvalidOperationException">Sequence row insertion fails.</exception>
    private static void InsertSequences(IntPtr database, string version)
    {
        var numeric = Version.Parse(version.Split('-', '+')[0]);
        var msiVersion = $"{numeric.Major}.{numeric.Minor}.{numeric.Build}";

        var prevUpgradeSql = "INSERT INTO Upgrade (UpgradeCode, VersionMin, VersionMax, Language, Attributes, Remove, ActionProperty) "
            + $"VALUES ('{UpgradeCode}', '', '{msiVersion}', '', {UpgradeAttributeVersionMaxInclusive}, '', 'PREVIOUSVERSIONSINSTALLED')";
        Execute(database, prevUpgradeSql);

        var nextUpgradeSql = "INSERT INTO Upgrade (UpgradeCode, VersionMin, VersionMax, Language, Attributes, Remove, ActionProperty) "
            + $"VALUES ('{UpgradeCode}', '{msiVersion}', '', '', {UpgradeAttributeVersionMinInclusive}, '', 'NEWERPRODUCTFOUND')";
        Execute(database, nextUpgradeSql);

        InsertExecuteSequence(database);
        InsertUiAndAdminSequences(database);
    }

    /// <summary>Inserts execution sequence actions.</summary>
    /// <param name="database">Database handle.</param>
    /// <exception cref="InvalidOperationException">Sequence action insertion fails.</exception>
    private static void InsertExecuteSequence(IntPtr database)
    {
        (string Action, string Condition, int Sequence)[] executeSequence =
        [
            ("FindRelatedProducts", string.Empty, SeqFindRelatedProducts),
            ("AppSearch", string.Empty, SeqAppSearch),
            ("ValidateProductID", string.Empty, SeqValidateProductId),
            (ActionCostInitialize, string.Empty, SeqCostInitialize),
            (ActionFileCost, string.Empty, SeqFileCost),
            (ActionCostFinalize, string.Empty, SeqCostFinalize),
            ("InstallValidate", string.Empty, SeqInstallValidate),

            // Windows Installer has no RemovePreviousVersions action; only this standard action removes the older product during an upgrade.
            ("RemoveExistingProducts", "PREVIOUSVERSIONSINSTALLED", SeqRemoveExistingProducts),
            ("InstallInitialize", string.Empty, SeqInstallInitialize),
            ("ProcessComponents", string.Empty, SeqProcessComponents),
            ("UnpublishFeatures", string.Empty, SeqUnpublishFeatures),
            ("RemoveShortcuts", string.Empty, SeqRemoveShortcuts),
            ("RemoveFiles", string.Empty, SeqRemoveFiles),
            ("InstallFiles", string.Empty, SeqInstallFiles),
            ("CreateShortcuts", string.Empty, SeqCreateShortcuts),
            ("RegisterUser", string.Empty, SeqRegisterUser),
            ("RegisterProduct", string.Empty, SeqRegisterProduct),
            ("PublishFeatures", string.Empty, SeqPublishFeatures),
            ("PublishProduct", string.Empty, SeqPublishProduct),
            ("InstallFinalize", string.Empty, SeqInstallFinalize),
        ];

        foreach (var (action, condition, seq) in executeSequence)
        {
            Execute(database, $"INSERT INTO InstallExecuteSequence (Action, Condition, Sequence) VALUES ('{action}', '{condition}', {seq})");
        }
    }

    /// <summary>Inserts UI, admin and advertising sequences.</summary>
    /// <param name="database">Database handle.</param>
    /// <exception cref="InvalidOperationException">Sequence action insertion fails.</exception>
    private static void InsertUiAndAdminSequences(IntPtr database)
    {
        (string Action, string Condition, int Sequence)[] uiSequence =
        [
            ("FindRelatedProducts", string.Empty, SeqFindRelatedProducts),
            (ActionCostInitialize, string.Empty, SeqCostInitialize),
            (ActionFileCost, string.Empty, SeqFileCost),
            (ActionCostFinalize, string.Empty, SeqCostFinalize),
            ("ExecuteAction", string.Empty, SeqExecuteAction),
        ];

        foreach (var (action, condition, seq) in uiSequence)
        {
            Execute(database, $"INSERT INTO InstallUISequence (Action, Condition, Sequence) VALUES ('{action}', '{condition}', {seq})");
        }

        (string Action, string Condition, int Sequence)[] adminSequence =
        [
            (ActionCostInitialize, string.Empty, SeqCostInitialize),
            (ActionFileCost, string.Empty, SeqFileCost),
            (ActionCostFinalize, string.Empty, SeqCostFinalize),
            ("InstallFiles", string.Empty, SeqInstallFiles),
            ("InstallFinalize", string.Empty, SeqInstallFinalize),
        ];

        foreach (var (action, condition, seq) in adminSequence)
        {
            Execute(database, $"INSERT INTO AdminExecuteSequence (Action, Condition, Sequence) VALUES ('{action}', '{condition}', {seq})");
        }

        (string Action, string Condition, int Sequence)[] advtSequence =
        [
            (ActionCostInitialize, string.Empty, SeqCostInitialize),
            (ActionCostFinalize, string.Empty, SeqCostFinalize),
            ("PublishProduct", string.Empty, SeqPublishProduct),
        ];

        foreach (var (action, condition, seq) in advtSequence)
        {
            Execute(database, $"INSERT INTO AdvtExecuteSequence (Action, Condition, Sequence) VALUES ('{action}', '{condition}', {seq})");
        }
    }

    /// <summary>Writes summary information properties.</summary>
    /// <param name="database">Database handle.</param>
    /// <param name="version">Application version string.</param>
    /// <param name="rid">Runtime identifier.</param>
    /// <exception cref="InvalidOperationException">Writing summary info fails.</exception>
    private static void SetSummaryInformation(IntPtr database, string version, string rid)
    {
        CheckStatus(NativeMethods.MsiGetSummaryInformation(database, null, MaxSummaryProperties, out var summary), "MsiGetSummaryInformation");
        try
        {
            var arch = rid == "win-arm64" ? "Arm64" : "x64";
            var packageGuid = CreateDeterministicGuid($"Package-{version}-{rid}").ToString("B").ToUpperInvariant();

            CheckStatus(NativeMethods.MsiSummaryInfoSetProperty(summary, PidCodepage, VtI2, DefaultCodepage, IntPtr.Zero, null), "SetCodepage");
            CheckStatus(NativeMethods.MsiSummaryInfoSetProperty(summary, PidTitle, VtLpStr, 0, IntPtr.Zero, "Installation Database"), "SetTitle");
            CheckStatus(NativeMethods.MsiSummaryInfoSetProperty(summary, PidSubject, VtLpStr, 0, IntPtr.Zero, "Hyper PDF Viewer"), "SetSubject");
            CheckStatus(NativeMethods.MsiSummaryInfoSetProperty(summary, PidAuthor, VtLpStr, 0, IntPtr.Zero, "Glenn Watson"), "SetAuthor");
            CheckStatus(NativeMethods.MsiSummaryInfoSetProperty(summary, PidKeywords, VtLpStr, 0, IntPtr.Zero, "Installer"), "SetKeywords");
            CheckStatus(NativeMethods.MsiSummaryInfoSetProperty(summary, PidComments, VtLpStr, 0, IntPtr.Zero, "Hyper PDF Viewer Installer"), "SetComments");
            CheckStatus(NativeMethods.MsiSummaryInfoSetProperty(summary, PidTemplate, VtLpStr, 0, IntPtr.Zero, $"{arch};1033"), "SetTemplate");
            CheckStatus(NativeMethods.MsiSummaryInfoSetProperty(summary, PidRevNumber, VtLpStr, 0, IntPtr.Zero, packageGuid), "SetRevNumber");
            CheckStatus(NativeMethods.MsiSummaryInfoSetProperty(summary, PidPageCount, VtI4, MinimumInstallerVersion, IntPtr.Zero, null), "SetPageCount");
            CheckStatus(NativeMethods.MsiSummaryInfoSetProperty(summary, PidWordCount, VtI4, CompressedSourceWordCount, IntPtr.Zero, null), "SetWordCount");
            CheckStatus(NativeMethods.MsiSummaryInfoSetProperty(summary, PidAppName, VtLpStr, 0, IntPtr.Zero, "Windows Installer"), "SetAppName");

            CheckStatus(NativeMethods.MsiSummaryInfoPersist(summary), "MsiSummaryInfoPersist");
        }
        finally
        {
            _ = NativeMethods.MsiCloseHandle(summary);
        }
    }

    /// <summary>Executes a SQL statement on the database.</summary>
    /// <param name="database">Database handle.</param>
    /// <param name="sql">SQL statement to execute.</param>
    /// <exception cref="InvalidOperationException">The SQL execution fails.</exception>
    private static void Execute(IntPtr database, string sql)
    {
        CheckStatus(NativeMethods.MsiDatabaseOpenView(database, sql, out var view), $"OpenView: {sql}");
        try
        {
            CheckStatus(NativeMethods.MsiViewExecute(view, IntPtr.Zero), $"Execute: {sql}");
            CheckStatus(NativeMethods.MsiViewClose(view), $"CloseView: {sql}");
        }
        finally
        {
            _ = NativeMethods.MsiCloseHandle(view);
        }
    }

    /// <summary>Executes a SQL statement that attaches a stream parameter.</summary>
    /// <param name="database">Database handle.</param>
    /// <param name="sql">SQL statement with parameter.</param>
    /// <param name="filePath">File to stream.</param>
    /// <exception cref="InvalidOperationException">Streaming fails.</exception>
    private static void ExecuteWithStream(IntPtr database, string sql, string filePath)
    {
        CheckStatus(NativeMethods.MsiDatabaseOpenView(database, sql, out var view), $"OpenView: {sql}");
        var record = NativeMethods.MsiCreateRecord(StreamRecordParamCount);
        try
        {
            CheckStatus(NativeMethods.MsiRecordSetStream(record, StreamRecordField, filePath), $"SetStream: {filePath}");
            CheckStatus(NativeMethods.MsiViewExecute(view, record), $"ExecuteWithStream: {sql}");
            CheckStatus(NativeMethods.MsiViewClose(view), $"CloseView: {sql}");
        }
        finally
        {
            _ = NativeMethods.MsiCloseHandle(record);
            _ = NativeMethods.MsiCloseHandle(view);
        }
    }

    /// <summary>Validates the return code from an MSI API call.</summary>
    /// <param name="code">Error code returned by the API.</param>
    /// <param name="operation">Description of the operation.</param>
    /// <exception cref="InvalidOperationException">The code is not zero.</exception>
    private static void CheckStatus(uint code, string operation)
    {
        if (code != 0)
        {
            throw new InvalidOperationException($"{operation} failed with error code {code}.");
        }
    }

    /// <summary>Escapes single quotes for SQL string literals.</summary>
    /// <param name="value">String to escape.</param>
    /// <returns>Escaped string.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string EscapeSql(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    /// <summary>Creates a deterministic GUID from a string identifier.</summary>
    /// <param name="input">Input string.</param>
    /// <returns>Deterministic GUID.</returns>
    private static Guid CreateDeterministicGuid(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return new(hash.AsSpan(0, GuidByteLength));
    }

    /// <summary>P/Invoke definitions for MSI APIs.</summary>
    private static partial class NativeMethods
    {
        /// <summary>Opens or creates a Windows Installer database.</summary>
        /// <param name="databasePath">The path to the database.</param>
        /// <param name="persistMode">The persistence mode.</param>
        /// <param name="databaseHandle">Output handle to the database.</param>
        /// <returns>A Windows error code.</returns>
        [LibraryImport("msi.dll", EntryPoint = "MsiOpenDatabaseW", StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial uint MsiOpenDatabase(string databasePath, IntPtr persistMode, out IntPtr databaseHandle);

        /// <summary>Prepares a database query and creates a view object.</summary>
        /// <param name="databaseHandle">The database handle.</param>
        /// <param name="query">The SQL query string.</param>
        /// <param name="viewHandle">Output handle to the view.</param>
        /// <returns>A Windows error code.</returns>
        [LibraryImport("msi.dll", EntryPoint = "MsiDatabaseOpenViewW", StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial uint MsiDatabaseOpenView(IntPtr databaseHandle, string query, out IntPtr viewHandle);

        /// <summary>Executes a database view query.</summary>
        /// <param name="viewHandle">The view handle.</param>
        /// <param name="recordHandle">The record handle containing parameters, or Zero.</param>
        /// <returns>A Windows error code.</returns>
        [LibraryImport("msi.dll", EntryPoint = "MsiViewExecute")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial uint MsiViewExecute(IntPtr viewHandle, IntPtr recordHandle);

        /// <summary>Closes a database view.</summary>
        /// <param name="viewHandle">The view handle.</param>
        /// <returns>A Windows error code.</returns>
        [LibraryImport("msi.dll", EntryPoint = "MsiViewClose")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial uint MsiViewClose(IntPtr viewHandle);

        /// <summary>Closes an open Windows Installer handle.</summary>
        /// <param name="handle">The handle to close.</param>
        /// <returns>A Windows error code.</returns>
        [LibraryImport("msi.dll", EntryPoint = "MsiCloseHandle")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial uint MsiCloseHandle(IntPtr handle);

        /// <summary>Commits changes to the database.</summary>
        /// <param name="databaseHandle">The database handle.</param>
        /// <returns>A Windows error code.</returns>
        [LibraryImport("msi.dll", EntryPoint = "MsiDatabaseCommit")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial uint MsiDatabaseCommit(IntPtr databaseHandle);

        /// <summary>Creates a new record object with specified fields.</summary>
        /// <param name="parameterCount">The number of fields.</param>
        /// <returns>The record handle.</returns>
        [LibraryImport("msi.dll", EntryPoint = "MsiCreateRecord")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial IntPtr MsiCreateRecord(uint parameterCount);

        /// <summary>Sets a record stream field from a file.</summary>
        /// <param name="recordHandle">The record handle.</param>
        /// <param name="fieldIndex">The 1-based field index.</param>
        /// <param name="filePath">Path to the file to stream.</param>
        /// <returns>A Windows error code.</returns>
        [LibraryImport("msi.dll", EntryPoint = "MsiRecordSetStreamW", StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial uint MsiRecordSetStream(IntPtr recordHandle, uint fieldIndex, string filePath);

        /// <summary>Gets a handle to the summary information stream.</summary>
        /// <param name="databaseHandle">The database handle.</param>
        /// <param name="databasePath">The database path, or null.</param>
        /// <param name="updateCount">Maximum number of updated properties.</param>
        /// <param name="summaryInfoHandle">Output handle to summary information.</param>
        /// <returns>A Windows error code.</returns>
        [LibraryImport("msi.dll", EntryPoint = "MsiGetSummaryInformationW", StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial uint MsiGetSummaryInformation(IntPtr databaseHandle, string? databasePath, uint updateCount, out IntPtr summaryInfoHandle);

        /// <summary>Sets a property in the summary information stream.</summary>
        /// <param name="summaryInfoHandle">The summary information handle.</param>
        /// <param name="propertyId">The property PID.</param>
        /// <param name="dataType">The property data type.</param>
        /// <param name="integerValue">Integer value if integer property.</param>
        /// <param name="fileTimeValue">FileTime pointer if date property.</param>
        /// <param name="stringValue">String value if text property.</param>
        /// <returns>A Windows error code.</returns>
        [LibraryImport("msi.dll", EntryPoint = "MsiSummaryInfoSetPropertyW", StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial uint MsiSummaryInfoSetProperty(IntPtr summaryInfoHandle, uint propertyId, uint dataType, int integerValue, IntPtr fileTimeValue, string? stringValue);

        /// <summary>Writes the summary information to the database.</summary>
        /// <param name="summaryInfoHandle">The summary information handle.</param>
        /// <returns>A Windows error code.</returns>
        [LibraryImport("msi.dll", EntryPoint = "MsiSummaryInfoPersist")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial uint MsiSummaryInfoPersist(IntPtr summaryInfoHandle);
    }
}
