// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if WINDOWS
using System.Runtime.InteropServices;

namespace PdfViewerLite.Tools.Commands;
#endif

#if WINDOWS
/// <summary>The native Windows Installer entry points used by package checks.</summary>
internal static partial class NativeMethods
{
    /// <summary>Installs or removes an MSI package.</summary>
    /// <param name="path">The package path.</param>
    /// <param name="properties">The installer properties.</param>
    /// <returns>The Windows Installer result code.</returns>
    [LibraryImport("msi.dll", EntryPoint = "MsiInstallProductW", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint MsiInstallProduct(string path, string properties);

    /// <summary>Chooses the Windows Installer UI level.</summary>
    /// <param name="level">The UI level.</param>
    /// <param name="owner">The owner window.</param>
    /// <returns>The previous UI level.</returns>
    [LibraryImport("msi.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint MsiSetInternalUI(uint level, IntPtr owner);

    /// <summary>Enables native installer diagnostics.</summary>
    /// <param name="mode">The log categories.</param>
    /// <param name="file">The log path.</param>
    /// <param name="attributes">The log attributes.</param>
    /// <returns>The result code.</returns>
    [LibraryImport("msi.dll", EntryPoint = "MsiEnableLogW", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint MsiEnableLog(uint mode, string file, uint attributes);
}
#endif
