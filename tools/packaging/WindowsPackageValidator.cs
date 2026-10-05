// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace PdfViewerLite.Tools.Packaging;

/// <summary>Checks installer formats with the Windows package readers.</summary>
internal static partial class WindowsPackageValidator
{
    /// <summary>The package factory ABI used by the Windows reader.</summary>
    [GeneratedComInterface]
    [Guid("beb94909-e451-438b-b5a7-d79e767b75d8")]
    internal partial interface IAppxFactory
    {
        /// <summary>Creates a package writer.</summary>
        /// <param name="stream">The output stream.</param>
        /// <param name="settings">The package settings.</param>
        /// <param name="writer">The returned writer.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int CreatePackageWriter(IntPtr stream, IntPtr settings, out IntPtr writer);

        /// <summary>Creates a package reader.</summary>
        /// <param name="stream">The input stream.</param>
        /// <param name="reader">The returned reader.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int CreatePackageReader(IntPtr stream, out IntPtr reader);
    }

    /// <summary>Validates an MSIX without installing it.</summary>
    /// <param name="path">The package path.</param>
    internal static void ValidateMsix(string path)
    {
        Marshal.ThrowExceptionForHR(NativeMethods.CoInitializeEx(IntPtr.Zero, 0));
        try
        {
            var factoryClass = new Guid("5842a140-ff9f-4166-8f5c-62f5b7b0c781");
            var factoryInterface = new Guid("beb94909-e451-438b-b5a7-d79e767b75d8");
            Marshal.ThrowExceptionForHR(NativeMethods.CoCreateInstance(in factoryClass, IntPtr.Zero, 1, in factoryInterface, out var factory));
            Marshal.ThrowExceptionForHR(NativeMethods.SHCreateStreamOnFileEx(path, 0x20, 0, 0, IntPtr.Zero, out var stream));
            try
            {
                Marshal.ThrowExceptionForHR(factory.CreatePackageReader(stream, out var reader));
                _ = Marshal.Release(reader);
            }
            finally
            {
                _ = Marshal.Release(stream);
            }
        }
        finally
        {
            NativeMethods.CoUninitialize();
        }

        Console.WriteLine($"Windows accepted the MSIX format: {path}");
    }

    /// <summary>Validates an MSI without installing it.</summary>
    /// <param name="path">The package path.</param>
    /// <exception cref="InvalidDataException">Windows cannot open the installer.</exception>
    internal static void ValidateMsi(string path)
    {
        var result = NativeMethods.MsiOpenPackageEx(path, 1, out var handle);
        if (result != 0)
        {
            throw new InvalidDataException($"Windows rejected the MSI {path} with error {result}.");
        }

        _ = NativeMethods.MsiCloseHandle(handle);
        Console.WriteLine($"Windows accepted the MSI format: {path}");
    }

    /// <summary>The Windows package API imports.</summary>
    private static partial class NativeMethods
    {
        /// <summary>Initializes the COM apartment.</summary>
        /// <param name="reserved">The reserved null pointer.</param>
        /// <param name="mode">The apartment model.</param>
        /// <returns>The HRESULT.</returns>
        [LibraryImport("ole32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial int CoInitializeEx(IntPtr reserved, uint mode);

        /// <summary>Releases the COM apartment.</summary>
        [LibraryImport("ole32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial void CoUninitialize();

        /// <summary>Creates the Windows package factory.</summary>
        [LibraryImport("ole32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial int CoCreateInstance(in Guid classId, IntPtr outer, uint context, in Guid interfaceId, out IAppxFactory factory);

        /// <summary>Opens a package as a native stream.</summary>
        [LibraryImport("shlwapi.dll", StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial int SHCreateStreamOnFileEx(string path, uint mode, uint attributes, int create, IntPtr template, out IntPtr stream);

        /// <summary>Opens an installer session without changing machine state.</summary>
        [LibraryImport("msi.dll", EntryPoint = "MsiOpenPackageExW", StringMarshalling = StringMarshalling.Utf16)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial uint MsiOpenPackageEx(string path, uint options, out uint handle);

        /// <summary>Closes the installer session.</summary>
        /// <param name="handle">The installer handle.</param>
        /// <returns>The Windows error code.</returns>
        [LibraryImport("msi.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        internal static partial uint MsiCloseHandle(uint handle);
    }
}
