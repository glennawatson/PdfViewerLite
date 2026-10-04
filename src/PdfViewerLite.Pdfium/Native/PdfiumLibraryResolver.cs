// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>
/// Loads PDFium by absolute path from the application directory (or, in development builds, from the NuGet
/// <c>runtimes/&lt;rid&gt;/native</c> layout), so the library is found without searching the working directory or
/// other unsafe locations.
/// </summary>
internal static class PdfiumLibraryResolver
{
    /// <summary>The library name used by <see cref="NativeMethods"/>.</summary>
    private const string LibraryName = "pdfium";

    /// <summary>Installs the resolver for this assembly. Safe to call more than once.</summary>
    internal static void Install()
    {
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(PdfiumLibraryResolver).Assembly, Resolve);
        }
        catch (InvalidOperationException)
        {
            // A resolver is already installed for this assembly.
        }
    }

    /// <summary>Resolves the PDFium library.</summary>
    /// <param name="libraryName">The requested library.</param>
    /// <param name="assembly">The requesting assembly.</param>
    /// <param name="searchPath">The search path flags.</param>
    /// <returns>The library handle, or zero to fall back to the default probing.</returns>
    private static nint Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibraryName)
        {
            return 0;
        }

        var fileName = GetFileName();
        var baseDirectory = AppContext.BaseDirectory;
        if (NativeLibrary.TryLoad(Path.Combine(baseDirectory, fileName), out var handle))
        {
            return handle;
        }

        var runtimePath = Path.Combine(baseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", fileName);
        return NativeLibrary.TryLoad(runtimePath, out handle) ? handle : 0;
    }

    /// <summary>Gets the platform specific file name.</summary>
    /// <returns>The file name.</returns>
    private static string GetFileName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "pdfium.dll";
        }

        return OperatingSystem.IsMacOS() ? "libpdfium.dylib" : "libpdfium.so";
    }
}
