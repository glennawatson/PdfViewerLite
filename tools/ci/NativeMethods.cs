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

    /// <summary>Gets one installed product that shares an upgrade code.</summary>
    /// <param name="upgradeCode">The upgrade code.</param>
    /// <param name="reserved">Must be zero.</param>
    /// <param name="index">The zero-based product index.</param>
    /// <param name="productCode">A 39 character buffer that receives the product code.</param>
    /// <returns>The result code; 259 when there are no more products.</returns>
    [LibraryImport("msi.dll", EntryPoint = "MsiEnumRelatedProductsW", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint MsiEnumRelatedProducts(string upgradeCode, uint reserved, uint index, Span<char> productCode);

    /// <summary>Installs or removes an installed product.</summary>
    /// <param name="productCode">The product code.</param>
    /// <param name="installLevel">The install level; zero for the default.</param>
    /// <param name="installState">The requested state; 2 removes the product.</param>
    /// <param name="commandLine">The installer properties.</param>
    /// <returns>The Windows Installer result code.</returns>
    [LibraryImport("msi.dll", EntryPoint = "MsiConfigureProductExW", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static partial uint MsiConfigureProductEx(string productCode, int installLevel, int installState, string commandLine);
}
#endif
