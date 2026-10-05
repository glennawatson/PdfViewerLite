// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO.Compression;
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
        int CreatePackageReader(IntPtr stream, out IAppxPackageReader reader);
    }

    /// <summary>The native package reader ABI.</summary>
    [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
    [Guid("b5c49650-99bc-481c-9a34-3d53a4106708")]
    internal partial interface IAppxPackageReader
    {
        /// <summary>Gets the block map reader.</summary>
        /// <param name="reader">The returned reader.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int GetBlockMap(out IAppxBlockMapReader reader);

        /// <summary>Gets a metadata file.</summary>
        /// <param name="type">The metadata file kind.</param>
        /// <param name="file">The returned file.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int GetFootprintFile(int type, out IAppxFile file);

        /// <summary>Gets a payload file.</summary>
        /// <param name="name">The package file name.</param>
        /// <param name="file">The returned file.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int GetPayloadFile(string name, out IAppxFile file);
    }

    /// <summary>The native package file ABI.</summary>
    [GeneratedComInterface]
    [Guid("91df827b-94fd-468f-827b-57f41b2f6f2e")]
    internal partial interface IAppxFile
    {
        /// <summary>Gets the compression method.</summary>
        /// <param name="option">The compression method.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int GetCompressionOption(out uint option);

        /// <summary>Gets the allocated content type.</summary>
        /// <param name="contentType">The allocated string.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int GetContentType(out IntPtr contentType);

        /// <summary>Gets the allocated file name.</summary>
        /// <param name="name">The allocated string.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int GetName(out IntPtr name);

        /// <summary>Gets the uncompressed size.</summary>
        /// <param name="size">The file size.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int GetSize(out ulong size);

        /// <summary>Gets the decompressed file stream.</summary>
        /// <param name="stream">The returned native stream.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int GetStream(out IntPtr stream);
    }

    /// <summary>The native block map reader ABI.</summary>
    [GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
    [Guid("5efec991-bca3-42d1-9ec2-e92d609ec22a")]
    internal partial interface IAppxBlockMapReader
    {
        /// <summary>Gets a file's block map.</summary>
        /// <param name="name">The package file name.</param>
        /// <param name="file">The returned block map.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int GetFile(string name, out IAppxBlockMapFile file);
    }

    /// <summary>The native per-file block map ABI.</summary>
    [GeneratedComInterface]
    [Guid("277672ac-4f63-42c1-8abc-beae3600eb59")]
    internal partial interface IAppxBlockMapFile
    {
        /// <summary>Gets the hash block enumerator.</summary>
        /// <param name="blocks">The returned enumerator.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int GetBlocks(out IntPtr blocks);

        /// <summary>Gets the local ZIP header size.</summary>
        /// <param name="size">The header size.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int GetLocalFileHeaderSize(out uint size);

        /// <summary>Gets the allocated file name.</summary>
        /// <param name="name">The allocated string.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int GetName(out IntPtr name);

        /// <summary>Gets the uncompressed file size.</summary>
        /// <param name="size">The file size.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int GetUncompressedSize(out ulong size);

        /// <summary>Validates each hash block against the decompressed stream.</summary>
        /// <param name="stream">The decompressed stream.</param>
        /// <param name="valid">Whether all blocks match.</param>
        /// <returns>The HRESULT.</returns>
        [PreserveSig]
        int ValidateFileHash(IntPtr stream, out int valid);
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
                ValidateBlocks(reader, path);
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

    /// <summary>Validates all mapped files with the native decompressor and block map reader.</summary>
    /// <param name="reader">The Windows package reader.</param>
    /// <param name="path">The package path.</param>
    /// <exception cref="InvalidDataException">A payload hash does not match its block map.</exception>
    private static void ValidateBlocks(IAppxPackageReader reader, string path)
    {
        Marshal.ThrowExceptionForHR(reader.GetBlockMap(out var map));
        using var archive = ZipFile.OpenRead(path);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName is "AppxBlockMap.xml" or "[Content_Types].xml" or "AppxSignature.p7x")
            {
                continue;
            }

            var name = entry.FullName.Replace('/', '\\');
            Console.WriteLine($"Checking Windows MSIX block hashes: {name}");
            IAppxFile file;
            Marshal.ThrowExceptionForHR(entry.FullName == "AppxManifest.xml" ? reader.GetFootprintFile(0, out file) : reader.GetPayloadFile(name, out file));
            Marshal.ThrowExceptionForHR(file.GetStream(out var stream));
            try
            {
                Marshal.ThrowExceptionForHR(map.GetFile(name, out var blocks));
                Marshal.ThrowExceptionForHR(blocks.ValidateFileHash(stream, out var valid));
                if (valid == 0)
                {
                    throw new InvalidDataException($"Windows rejected the MSIX block hashes for {name}.");
                }
            }
            finally
            {
                _ = Marshal.Release(stream);
            }
        }
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
